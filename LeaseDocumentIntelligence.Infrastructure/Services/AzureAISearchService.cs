namespace LeaseDocumentIntelligence.Infrastructure.Services;

using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using LeaseDocumentIntelligence.Domain.DTOs;
using LeaseDocumentIntelligence.Domain.Interfaces;
using LeaseDocumentIntelligence.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

/// <summary>
/// Azure AI Search service with vector embeddings and RAG capabilities.
/// </summary>
public class AzureAISearchService : IVectorSearchService
{
    private readonly SearchIndexClient _indexClient;
    private readonly IEmbeddingService _embeddingService;
    private readonly IFoundryAIService _foundryAI;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AzureAISearchService> _logger;
    private readonly string _indexName;

    private const int VectorDimensions = 1536; // text-embedding-ada-002

    public AzureAISearchService(
        IEmbeddingService embeddingService,
        IFoundryAIService foundryAI,
        IConfiguration configuration,
        ILogger<AzureAISearchService> logger)
    {
        _embeddingService = embeddingService;
        _foundryAI = foundryAI;
        _configuration = configuration;
        _logger = logger;

        var endpoint = configuration["AzureAISearch:Endpoint"]
            ?? throw new InvalidOperationException("AzureAISearch:Endpoint not configured");
        _indexName = configuration["AzureAISearch:IndexName"] ?? "lease-documents";

        // Use API Key if configured, otherwise Managed Identity
        var apiKey = configuration["AzureAISearch:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            _indexClient = new SearchIndexClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
        }
        else
        {
            _indexClient = new SearchIndexClient(new Uri(endpoint), new DefaultAzureCredential());
        }
    }

    public async Task EnsureIndexExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var index = new SearchIndex(_indexName)
            {
                Fields =
                [
                    new SimpleField("id", SearchFieldDataType.String) { IsKey = true },
                    new SearchableField("fileName") { IsFilterable = true },
                    new SearchableField("tenantName") { IsFilterable = true, IsFacetable = true },
                    new SearchableField("propertyName") { IsFilterable = true, IsFacetable = true },
                    new SearchableField("documentText") { AnalyzerName = LexicalAnalyzerName.EnMicrosoft },
                    new SearchableField("extractedFieldsText"),
                    new SimpleField("uploadedAt", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
                    new SimpleField("status", SearchFieldDataType.String) { IsFilterable = true },
                    new SimpleField("extractedFieldsJson", SearchFieldDataType.String),
                    // Vector field for semantic search
                    new VectorSearchField("contentVector", VectorDimensions, "vector-profile")
                ],
                VectorSearch = new VectorSearch
                {
                    Profiles =
                    {
                        new VectorSearchProfile("vector-profile", "vector-algorithm")
                    },
                    Algorithms =
                    {
                        new HnswAlgorithmConfiguration("vector-algorithm")
                        {
                            Parameters = new HnswParameters
                            {
                                Metric = VectorSearchAlgorithmMetric.Cosine,
                                M = 4,
                                EfConstruction = 400,
                                EfSearch = 500
                            }
                        }
                    }
                },
                SemanticSearch = new SemanticSearch
                {
                    Configurations =
                    {
                        new SemanticConfiguration("semantic-config", new SemanticPrioritizedFields
                        {
                            TitleField = new SemanticField("fileName"),
                            ContentFields =
                            {
                                new SemanticField("documentText"),
                                new SemanticField("extractedFieldsText")
                            },
                            KeywordsFields =
                            {
                                new SemanticField("tenantName"),
                                new SemanticField("propertyName")
                            }
                        })
                    }
                }
            };

            await _indexClient.CreateOrUpdateIndexAsync(index, cancellationToken: cancellationToken);
            _logger.LogInformation("Search index '{IndexName}' ensured", _indexName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring search index exists");
            throw;
        }
    }

    public async Task IndexDocumentAsync(LeaseDocument document, string documentText, CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate embedding for document content
            var contentForEmbedding = BuildContentForEmbedding(document, documentText);
            var embedding = await _embeddingService.GenerateEmbeddingAsync(contentForEmbedding, cancellationToken);

            // Build searchable fields text
            var extractedFieldsText = string.Join(" | ", 
                document.ExtractedFields?.Select(f => $"{f.FieldName}: {f.Value}") ?? []);

            var searchDoc = new Dictionary<string, object>
            {
                ["id"] = document.Id,
                ["fileName"] = document.FileName ?? "",
                ["tenantName"] = GetFieldValue(document, "TenantName"),
                ["propertyName"] = GetFieldValue(document, "PropertyName"),
                ["documentText"] = TruncateText(documentText, 30000),
                ["extractedFieldsText"] = extractedFieldsText,
                ["uploadedAt"] = document.UploadedAt,
                ["status"] = document.Status.ToString(),
                ["extractedFieldsJson"] = JsonSerializer.Serialize(document.ExtractedFields ?? []),
                ["contentVector"] = embedding
            };

            var searchClient = _indexClient.GetSearchClient(_indexName);
            await searchClient.MergeOrUploadDocumentsAsync([searchDoc], cancellationToken: cancellationToken);

            _logger.LogInformation("Indexed document {DocumentId} in search", document.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing document {DocumentId}", document.Id);
            throw;
        }
    }

    public async Task<SearchResultDto> SearchAsync(string query, SearchOptions? options = null, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        options ??= new SearchOptions();

        try
        {
            var searchClient = _indexClient.GetSearchClient(_indexName);

            // Generate query embedding for vector search
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

            var searchOptions = new Azure.Search.Documents.SearchOptions
            {
                Size = options.Top,
                IncludeTotalCount = true,
                Select = { "id", "fileName", "tenantName", "propertyName", "status", "extractedFieldsJson" },
                HighlightFields = { "documentText", "extractedFieldsText" },
                HighlightPreTag = "<mark>",
                HighlightPostTag = "</mark>"
            };

            // Add vector query for hybrid search
            if (options.UseHybridSearch)
            {
                searchOptions.VectorSearch = new VectorSearchOptions
                {
                    Queries =
                    {
                        new VectorizedQuery(queryEmbedding)
                        {
                            KNearestNeighborsCount = options.Top,
                            Fields = { "contentVector" }
                        }
                    }
                };
            }

            // Add tenant filter if specified
            if (!string.IsNullOrEmpty(options.TenantFilter))
            {
                searchOptions.Filter = $"tenantName eq '{options.TenantFilter}'";
            }

            var response = await searchClient.SearchAsync<SearchDocument>(query, searchOptions, cancellationToken);

            var hits = new List<SearchHitDto>();
            await foreach (var result in response.Value.GetResultsAsync())
            {
                if (result.Score < options.MinScore) continue;

                var hit = new SearchHitDto
                {
                    DocumentId = result.Document["id"]?.ToString() ?? "",
                    FileName = result.Document["fileName"]?.ToString() ?? "",
                    TenantName = result.Document["tenantName"]?.ToString() ?? "",
                    PropertyName = result.Document["propertyName"]?.ToString() ?? "",
                    Score = result.Score ?? 0,
                    Highlights = result.Highlights?.SelectMany(h => h.Value).ToList() ?? []
                };

                // Parse extracted fields
                if (result.Document.TryGetValue("extractedFieldsJson", out var fieldsJson) && fieldsJson != null)
                {
                    try
                    {
                        var fields = JsonSerializer.Deserialize<List<ExtractedField>>(fieldsJson.ToString() ?? "[]");
                        hit.ExtractedFields = fields?.ToDictionary(f => f.FieldName, f => f.Value ?? "") ?? [];
                    }
                    catch { /* Ignore parse errors */ }
                }

                hits.Add(hit);
            }

            return new SearchResultDto
            {
                Query = query,
                TotalCount = (int)(response.Value.TotalCount ?? hits.Count),
                Hits = hits,
                SearchTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents");
            throw;
        }
    }

    public async Task<ReasonedSearchResultDto> SearchWithReasoningAsync(string query, SearchOptions? options = null, CancellationToken cancellationToken = default)
    {
        // First perform the search
        var searchResults = await SearchAsync(query, options, cancellationToken);

        // Build context from search results for reasoning
        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("SEARCH RESULTS FOR ANALYSIS:");
        contextBuilder.AppendLine();

        foreach (var hit in searchResults.Hits.Take(5))
        {
            contextBuilder.AppendLine($"Document: {hit.FileName}");
            contextBuilder.AppendLine($"Tenant: {hit.TenantName}");
            contextBuilder.AppendLine($"Property: {hit.PropertyName}");
            contextBuilder.AppendLine($"Relevance Score: {hit.Score:P0}");
            contextBuilder.AppendLine("Key Fields:");
            foreach (var field in hit.ExtractedFields.Take(10))
            {
                contextBuilder.AppendLine($"  - {field.Key}: {field.Value}");
            }
            if (hit.Highlights.Count > 0)
            {
                contextBuilder.AppendLine("Relevant Excerpts:");
                foreach (var highlight in hit.Highlights.Take(3))
                {
                    contextBuilder.AppendLine($"  \"{StripHtmlTags(highlight)}\"");
                }
            }
            contextBuilder.AppendLine();
        }

        // Generate reasoning using AI
        var reasoning = await GenerateReasoningAsync(query, contextBuilder.ToString(), searchResults.Hits, cancellationToken);

        return new ReasonedSearchResultDto
        {
            Query = query,
            SearchResults = searchResults,
            Summary = reasoning.Summary,
            Reasoning = reasoning.Reasoning,
            KeyInsights = reasoning.KeyInsights,
            SuggestedQuestions = reasoning.SuggestedQuestions,
            Citations = reasoning.Citations
        };
    }

    public async Task DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var searchClient = _indexClient.GetSearchClient(_indexName);
            await searchClient.DeleteDocumentsAsync("id", [documentId], cancellationToken: cancellationToken);
            _logger.LogInformation("Deleted document {DocumentId} from search index", documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document {DocumentId} from search", documentId);
            throw;
        }
    }

    private async Task<ReasoningResult> GenerateReasoningAsync(string query, string context, List<SearchHitDto> hits, CancellationToken cancellationToken)
    {
        var prompt = $@"You are a commercial lease analysis expert. A user searched for: ""{query}""

Based on the search results below, provide:
1. A concise summary (2-3 sentences) of what was found
2. Your reasoning about why these documents match and their relevance
3. 3-5 key insights from the matching leases
4. 2-3 suggested follow-up questions

{context}

Respond in JSON format:
{{
  ""summary"": ""...'",
  ""reasoning"": ""..."",
  ""keyInsights"": [""..."", ""...""],
  ""suggestedQuestions"": [""..."", ""...""]
}}";

        try
        {
            // Use existing Foundry AI service for reasoning
            var response = await CallReasoningModelAsync(prompt, cancellationToken);
            return ParseReasoningResponse(response, hits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating reasoning");
            return new ReasoningResult
            {
                Summary = $"Found {hits.Count} documents matching your search.",
                Reasoning = "Unable to generate detailed reasoning at this time.",
                KeyInsights = [],
                SuggestedQuestions = [],
                Citations = []
            };
        }
    }

    private async Task<string> CallReasoningModelAsync(string prompt, CancellationToken cancellationToken)
    {
        // Check for local testing mode
        if (_configuration.GetValue<bool>("Foundry:UseLocalTesting"))
        {
            return JsonSerializer.Serialize(new
            {
                summary = "Found multiple lease documents matching your criteria.",
                reasoning = "The search identified leases based on semantic similarity to your query.",
                keyInsights = new[] { "Multiple tenants have similar lease terms", "Rent escalation clauses vary by property" },
                suggestedQuestions = new[] { "What are the renewal terms?", "Which leases expire this year?" }
            });
        }

        var endpoint = _configuration["Foundry:Endpoint"] ?? "";
        var model = _configuration["Foundry:Model"] ?? "gpt-4o";

        using var httpClient = new HttpClient();
        var url = $"{endpoint.TrimEnd('/')}/openai/v1/responses";

        var request = new HttpRequestMessage(HttpMethod.Post, url);

        var apiKey = _configuration["Foundry:ApiKey"];
        if (!string.IsNullOrEmpty(apiKey))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }
        else
        {
            var credential = new DefaultAzureCredential();
            var token = await credential.GetTokenAsync(
                new Azure.Core.TokenRequestContext(["https://cognitiveservices.azure.com/.default"]),
                cancellationToken);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
        }

        var requestBody = new
        {
            model,
            input = prompt,
            max_output_tokens = 1000
        };

        request.Content = System.Net.Http.Json.JsonContent.Create(requestBody);

        var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(responseJson);
        if (doc.RootElement.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                if (item.TryGetProperty("type", out var type) && type.GetString() == "message")
                {
                    if (item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
                    {
                        var first = content[0];
                        if (first.TryGetProperty("text", out var text))
                        {
                            return text.GetString() ?? "";
                        }
                    }
                }
            }
        }

        return "";
    }

    private ReasoningResult ParseReasoningResponse(string response, List<SearchHitDto> hits)
    {
        try
        {
            // Extract JSON from response
            var jsonContent = response;
            if (response.Contains("```json"))
            {
                var start = response.IndexOf("```json") + 7;
                var end = response.IndexOf("```", start);
                if (end > start) jsonContent = response[start..end].Trim();
            }
            else if (response.Contains("```"))
            {
                var start = response.IndexOf("```") + 3;
                var end = response.IndexOf("```", start);
                if (end > start) jsonContent = response[start..end].Trim();
            }

            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            var result = new ReasoningResult
            {
                Summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "",
                Reasoning = root.TryGetProperty("reasoning", out var r) ? r.GetString() ?? "" : "",
                KeyInsights = root.TryGetProperty("keyInsights", out var ki) && ki.ValueKind == JsonValueKind.Array
                    ? ki.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                    : [],
                SuggestedQuestions = root.TryGetProperty("suggestedQuestions", out var sq) && sq.ValueKind == JsonValueKind.Array
                    ? sq.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                    : []
            };

            // Generate citations from hits
            result.Citations = hits.Take(3).Select(h => new CitationDto
            {
                DocumentId = h.DocumentId,
                FileName = h.FileName,
                Excerpt = h.Highlights.FirstOrDefault() ?? ""
            }).ToList();

            return result;
        }
        catch
        {
            return new ReasoningResult
            {
                Summary = "Search completed successfully.",
                Reasoning = response,
                KeyInsights = [],
                SuggestedQuestions = [],
                Citations = []
            };
        }
    }

    private static string BuildContentForEmbedding(LeaseDocument document, string documentText)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"File: {document.FileName}");

        if (document.ExtractedFields != null)
        {
            foreach (var field in document.ExtractedFields.Where(f => f.ConfidenceScore > 0.5))
            {
                builder.AppendLine($"{field.FieldName}: {field.Value}");
            }
        }

        // Include first portion of document text
        builder.AppendLine();
        builder.Append(TruncateText(documentText, 6000));

        return builder.ToString();
    }

    private static string GetFieldValue(LeaseDocument document, string fieldName)
    {
        return document.ExtractedFields?
            .FirstOrDefault(f => f.FieldName.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
            ?.Value ?? "";
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..maxLength];
    }

    private static string StripHtmlTags(string html)
    {
        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", "");
    }

    private class ReasoningResult
    {
        public string Summary { get; set; } = "";
        public string Reasoning { get; set; } = "";
        public List<string> KeyInsights { get; set; } = [];
        public List<string> SuggestedQuestions { get; set; } = [];
        public List<CitationDto> Citations { get; set; } = [];
    }
}
