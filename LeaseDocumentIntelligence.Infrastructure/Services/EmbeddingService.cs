namespace LeaseDocumentIntelligence.Infrastructure.Services;

using Azure.Identity;
using LeaseDocumentIntelligence.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;

/// <summary>
/// Service for generating embeddings using Azure OpenAI.
/// </summary>
public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly string _endpoint;
    private readonly string _deploymentName;

    public EmbeddingService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<EmbeddingService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("AzureOpenAI");
        _configuration = configuration;
        _logger = logger;
        _endpoint = configuration["AzureOpenAI:Endpoint"] 
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint not configured");
        _deploymentName = configuration["AzureOpenAI:EmbeddingDeployment"] ?? "text-embedding-ada-002";
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var embeddings = await GenerateEmbeddingsAsync([text], cancellationToken);
        return embeddings.FirstOrDefault() ?? [];
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var textList = texts.ToList();
        if (textList.Count == 0)
        {
            return [];
        }

        // Check for local testing mode
        if (_configuration.GetValue<bool>("AzureOpenAI:UseLocalTesting"))
        {
            return textList.Select(_ => GenerateMockEmbedding()).ToList();
        }

        try
        {
            var url = $"{_endpoint.TrimEnd('/')}/openai/deployments/{_deploymentName}/embeddings?api-version=2024-02-01";

            var request = new HttpRequestMessage(HttpMethod.Post, url);

            // Use API Key if configured, otherwise Managed Identity
            var apiKey = _configuration["AzureOpenAI:ApiKey"];
            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.Add("api-key", apiKey);
            }
            else
            {
                var credential = new DefaultAzureCredential();
                var token = await credential.GetTokenAsync(
                    new Azure.Core.TokenRequestContext(["https://cognitiveservices.azure.com/.default"]),
                    cancellationToken);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
            }

            var requestBody = new { input = textList };
            request.Content = JsonContent.Create(requestBody);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var embeddings = ParseEmbeddingsResponse(responseJson);

            _logger.LogInformation("Generated {Count} embeddings", embeddings.Count);
            return embeddings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embeddings");
            throw;
        }
    }

    private List<float[]> ParseEmbeddingsResponse(string responseJson)
    {
        var embeddings = new List<float[]>();

        using var doc = JsonDocument.Parse(responseJson);
        var data = doc.RootElement.GetProperty("data");

        foreach (var item in data.EnumerateArray())
        {
            var embedding = item.GetProperty("embedding");
            var vector = new float[embedding.GetArrayLength()];
            int i = 0;
            foreach (var value in embedding.EnumerateArray())
            {
                vector[i++] = value.GetSingle();
            }
            embeddings.Add(vector);
        }

        return embeddings;
    }

    private static float[] GenerateMockEmbedding()
    {
        // Return a mock 1536-dimension embedding (text-embedding-ada-002 size)
        var random = new Random();
        var embedding = new float[1536];
        for (int i = 0; i < 1536; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }
        // Normalize
        var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] /= magnitude;
        }
        return embedding;
    }
}
