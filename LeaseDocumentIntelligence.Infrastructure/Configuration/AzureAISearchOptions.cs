namespace LeaseDocumentIntelligence.Infrastructure.Configuration;

/// <summary>
/// Configuration options for Azure AI Search.
/// </summary>
public class AzureAISearchOptions
{
    public const string SectionName = "AzureAISearch";

    public string Endpoint { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string IndexName { get; set; } = "lease-documents";
}

/// <summary>
/// Configuration options for Azure OpenAI embeddings.
/// </summary>
public class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    public string Endpoint { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string EmbeddingDeployment { get; set; } = "text-embedding-ada-002";
    public bool UseLocalTesting { get; set; } = false;
}
