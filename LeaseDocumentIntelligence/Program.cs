using Azure.Identity;
using LeaseDocumentIntelligence.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Identity.Web;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add Azure Key Vault as configuration source for production secrets
// Key Vault secrets override appsettings.json values
// Naming convention: "AzureAd--ClientSecret" in KV maps to "AzureAd:ClientSecret" in config
var keyVaultUri = builder.Configuration["KeyVault:VaultUri"];

builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential());


// Configure logging: App Insights for all environments, file logging only for Development
builder.Logging.ClearProviders();

if (builder.Environment.IsDevelopment())
{
    // Development: Console logging for local debugging
    builder.Logging.AddConsole();
    builder.Logging.AddDebug();
}

// Application Insights - only enable if connection string is configured (not placeholder)
var appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
    {
        options.ConnectionString = appInsightsConnectionString;
    });
}

// Set minimum log level to Warning (only Warning and Error will be logged)
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// Add authentication with Microsoft Entra ID
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

// Configure OIDC options for Blazor Server
builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.Authority = $"https://login.microsoftonline.com/{builder.Configuration["AzureAd:TenantId"]}/v2.0";
    options.ClientId = builder.Configuration["AzureAd:ClientId"];
    options.ClientSecret = builder.Configuration["AzureAd:ClientSecret"];
    options.ResponseType = "code";
    options.SaveTokens = true;
    options.GetClaimsFromUserInfoEndpoint = true;

    // Add scopes
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.Scope.Add("offline_access");
});

// Add authorization with role-based policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"))
    .AddPolicy("LeaseAnalystOrAdmin", policy => policy.RequireRole("Admin", "LeaseAnalyst"))
    .AddPolicy("ReviewerOrAdmin", policy => policy.RequireRole("Admin", "Reviewer"))
    .AddPolicy("AllAuthenticatedUsers", policy => policy.RequireAuthenticatedUser());

// AuthenticationStateProvider for Blazor Server - required for AuthorizeView components
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Add Infrastructure layer services (Key Vault, Blob Storage, Cosmos DB, AI services)
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add Blazor support
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register HttpClient for Blazor components with cookie forwarding
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("LocalApi", (sp, client) =>
{
    client.BaseAddress = new Uri("https://localhost:7231/");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    UseCookies = false // Disable automatic cookies, we'll forward manually
});

// Create scoped HttpClient that forwards auth cookies from current request
builder.Services.AddScoped(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    var client = factory.CreateClient("LocalApi");

    // Forward cookies from the current HTTP context to outgoing API requests
    var httpContext = httpContextAccessor.HttpContext;
    if (httpContext?.Request.Cookies.Count > 0)
    {
        var cookieHeader = string.Join("; ", httpContext.Request.Cookies.Select(c => $"{c.Key}={c.Value}"));
        client.DefaultRequestHeaders.Add("Cookie", cookieHeader);
    }

    return client;
});

// Note: Semantic Kernel for AI workflow orchestration is documented in SEMANTIC_KERNEL_GUIDE.md
// To enable in production: uncomment SK registration and SemanticOrchestrationService usage
// See SEMANTIC_KERNEL_GUIDE.md for complete setup instructions

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

    // OpenAPI/Swagger - built-in in .NET 10, no Swashbuckle needed
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Map endpoints - allow anonymous for Blazor SignalR hub to establish connection
app.MapRazorComponents<LeaseDocumentIntelligence.Components.App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous();

app.MapGet("/login-challenge", async (HttpContext context) =>
{
    await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme);
});

app.MapGet("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync();
    context.Response.Redirect("/");
});

app.MapControllers();

app.Run();
