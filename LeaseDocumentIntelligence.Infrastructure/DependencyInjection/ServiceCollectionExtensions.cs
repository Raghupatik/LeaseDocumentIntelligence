namespace LeaseDocumentIntelligence.Infrastructure.DependencyInjection;

using Azure.Identity;
using LeaseDocumentIntelligence.Domain.Interfaces;
using LeaseDocumentIntelligence.Infrastructure.Data;
using LeaseDocumentIntelligence.Infrastructure.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering Infrastructure services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Infrastructure layer services with proper DI patterns
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configuration options
        services.Configure<KeyVaultOptions>(configuration.GetSection(KeyVaultOptions.SectionName));
        services.Configure<BlobStorageOptions>(configuration.GetSection(BlobStorageOptions.SectionName));
        services.Configure<CosmosDbOptions>(configuration.GetSection(CosmosDbOptions.SectionName));

        // Memory cache for Key Vault secret caching
        services.AddMemoryCache();

        // Key Vault - Scoped (dispose per request)
        services.AddScoped<ISecretProvider, KeyVaultSecretProvider>();

        // Blob Storage - Scoped (async disposable)
        services.AddScoped<IBlobStorageService, BlobStorageService>();

        // Cosmos DB - Singleton CosmosClient (recommended by Microsoft)
        services.AddSingleton(sp =>
        {
            var cosmosOptions = configuration.GetSection(CosmosDbOptions.SectionName).Get<CosmosDbOptions>()
                ?? throw new InvalidOperationException("CosmosDb configuration is missing");

            var clientOptions = new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                },
                ConnectionMode = ConnectionMode.Direct,
                MaxRetryAttemptsOnRateLimitedRequests = 5,
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30)
            };

            return new CosmosClient(
                cosmosOptions.Endpoint,
                new DefaultAzureCredential(),
                clientOptions);
        });

        // Repository - Scoped (uses singleton CosmosClient)
        services.AddScoped<IDocumentMetadataRepository, CosmosDocumentMetadataRepository>();
        services.AddScoped<IFieldDefinitionRepository, CosmosFieldDefinitionRepository>();

        // HttpClientFactory for FoundryAI and Azure OpenAI
        services.AddHttpClient("FoundryAI", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });
        services.AddHttpClient("AzureOpenAI", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(3);
        });

        // AI and processing services - Scoped
        services.AddScoped<IFoundryAIService, FoundryAIService>();
        services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
        services.AddScoped<IExtractionService, ExtractionService>();
        services.AddScoped<IReviewQueueService, ReviewQueueService>();

        // Embedding and Vector Search services
        services.AddScoped<IEmbeddingService, EmbeddingService>();
        services.AddScoped<IVectorSearchService, AzureAISearchService>();

        // Legacy in-memory repository (can be removed once Cosmos is fully integrated)
        services.AddScoped<ILeaseDocumentRepository, LeaseDocumentRepository>();

        return services;
    }

    /// <summary>
    /// Initializes Cosmos DB database and container
    /// </summary>
    public static async Task InitializeCosmosDbAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var options = configuration.GetSection(CosmosDbOptions.SectionName).Get<CosmosDbOptions>()
            ?? throw new InvalidOperationException("CosmosDb configuration is missing");

        // Create database if not exists
        var database = await cosmosClient.CreateDatabaseIfNotExistsAsync(
            options.DatabaseName,
            cancellationToken: cancellationToken);

        // Create container with partition key
        await database.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(options.ContainerName, "/partitionKey")
            {
                IndexingPolicy = new IndexingPolicy
                {
                    Automatic = true,
                    IndexingMode = IndexingMode.Consistent,
                    IncludedPaths = { new IncludedPath { Path = "/*" } },
                    ExcludedPaths = { new ExcludedPath { Path = "/\"_etag\"/?" } }
                }
            },
            cancellationToken: cancellationToken);
    }
}
