using LeaseDocumentIntelligence.Domain.Interfaces;

namespace LeaseDocumentIntelligence.Infrastructure.Services;

using Azure.Identity;
using LeaseDocumentIntelligence.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

public class FoundryAIService : IFoundryAIService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FoundryAIService> _logger;
    private readonly string _foundryEndpoint;
    private readonly string _modelName;
    private readonly IConfiguration _configuration;

    public FoundryAIService(
        IHttpClientFactory httpClientFactory,
        ILogger<FoundryAIService> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
        _foundryEndpoint = configuration["Foundry:Endpoint"] ?? string.Empty;
        _modelName = configuration["Foundry:ModelName"] ?? "gpt-4";
    }

    public async Task<List<ExtractedField>> ExtractFieldsAsync(
        string documentText,
        Dictionary<string, string> fieldDefinitions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = BuildExtractionPrompt(documentText, fieldDefinitions);
            var response = await CallFoundryModelAsync(prompt, cancellationToken);

            var fields = ParseExtractionResponse(response);
            return fields;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during field extraction");
            throw;
        }
    }

    /// <summary>
    /// Extracts fields using metadata-driven field definitions with extraction hints.
    /// </summary>
    public async Task<List<ExtractedField>> ExtractFieldsAsync(
        string documentText,
        List<FieldDefinition> fieldDefinitions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = BuildExtractionPromptFromMetadata(documentText, fieldDefinitions);
            var response = await CallFoundryModelAsync(prompt, cancellationToken);

            var fields = ParseExtractionResponse(response, fieldDefinitions);
            return fields;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during metadata-driven field extraction");
            throw;
        }
    }

    public async Task<(ExtractedField Field, double Confidence)> ValidateExtractionAsync(
        string fieldName,
        string extractedValue,
        string documentText,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = BuildValidationPrompt(fieldName, extractedValue, documentText);
            var response = await CallFoundryModelAsync(prompt, cancellationToken);

            var (field, confidence) = ParseValidationResponse(fieldName, extractedValue, response);
            return (field, confidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during validation");
            throw;
        }
    }

    private string BuildExtractionPrompt(string documentText, Dictionary<string, string> fieldDefinitions)
    {
        var fieldsList = string.Join("\n", fieldDefinitions.Select(f => $"- {f.Key}: {f.Value}"));

        return $@"Extract the following lease fields from the document. For each field, provide:
1. The extracted value
2. A confidence score (0-1)
3. The page reference
4. A clause reference or raw excerpt

Fields to extract:
{fieldsList}

Document Text:
{documentText}

Respond in JSON format with array of objects containing: fieldName, value, confidence, pageRef, clauseRef, excerpt";
    }

    /// <summary>
    /// Builds extraction prompt from metadata-driven field definitions with hints.
    /// </summary>
    private string BuildExtractionPromptFromMetadata(string documentText, List<FieldDefinition> fieldDefinitions)
    {
        var fieldsBuilder = new System.Text.StringBuilder();
        foreach (var field in fieldDefinitions.OrderBy(f => f.DisplayOrder))
        {
            fieldsBuilder.AppendLine($"- **{field.FieldName}** ({field.FieldType})");
            fieldsBuilder.AppendLine($"  Description: {field.DisplayName}");
            if (!string.IsNullOrEmpty(field.ExtractionHint))
            {
                fieldsBuilder.AppendLine($"  Hint: {field.ExtractionHint}");
            }
            fieldsBuilder.AppendLine($"  Required: {(field.IsRequired ? "Yes" : "No")}");
            fieldsBuilder.AppendLine();
        }

        return $@"You are a commercial lease abstraction expert. Extract the following fields from the lease document.

For each field, provide:
1. The extracted value (use null if not found)
2. A confidence score (0.0 to 1.0) indicating your certainty
3. The page number where you found this information
4. The exact clause reference (e.g., ""Section 3.1"") or a brief excerpt

Fields to extract:
{fieldsBuilder}

Document Text:
{documentText}

IMPORTANT:
- Use the hints provided to locate each field
- Be precise with dates (use ISO 8601 format: YYYY-MM-DD)
- For currency, include the amount without currency symbols
- If a field cannot be found, set value to null and confidence to 0
- Include the exact text excerpt that supports your extraction

Respond ONLY with a valid JSON array:
[
  {{
    ""fieldName"": ""FieldName"",
    ""value"": ""extracted value or null"",
    ""confidence"": 0.85,
    ""pageRef"": 3,
    ""clauseRef"": ""Section 3.1"",
    ""excerpt"": ""exact text from document""
  }}
]";
    }

    private string BuildValidationPrompt(string fieldName, string extractedValue, string documentText)
    {
        return $@"Validate the following extraction:
Field: {fieldName}
Extracted Value: {extractedValue}

Document Text:
{documentText}

Determine if the extraction is accurate and provide a confidence score (0-1).
Respond with JSON containing: isAccurate (boolean), confidence (0-1), pageRef, clauseRef, correctedValue (if needed)";
    }

    private async Task<string> CallFoundryModelAsync(string prompt, CancellationToken cancellationToken)
    {
        // Check for local testing mode
        var useLocalTesting = _configuration.GetValue<bool>("Foundry:UseLocalTesting");
        if (useLocalTesting)
        {
            return GetMockExtractionResponse();
        }

        try
        {
            // Azure OpenAI endpoint format: {endpoint}/openai/deployments/{model}/chat/completions?api-version={version}
            var apiVersion = _configuration["Foundry:ApiVersion"] ?? "2024-06-01";
            var url = $"{_foundryEndpoint.TrimEnd('/')}/openai/deployments/{_modelName}/chat/completions?api-version={apiVersion}";
            _logger.LogInformation("Calling Azure OpenAI: {Url}", url);
            var request = new HttpRequestMessage(HttpMethod.Post, url);

            // Use API Key if configured, otherwise use Managed Identity
            var apiKey = _configuration["Foundry:ApiKey"];
            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.Add("api-key", apiKey);
            }
            else
            {
                var tokenProvider = new DefaultAzureCredential();
                var token = await tokenProvider.GetTokenAsync(
                    new Azure.Core.TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" }),
                    cancellationToken);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
            }

            // Azure OpenAI chat completions format
            var payload = new
            {
                messages = new[]
                {
                    new { role = "system", content = "You are a lease document extraction AI. Extract information accurately and always provide citations." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.1,
                max_tokens = 4000
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");

            using var httpClient = _httpClientFactory.CreateClient("FoundryAI");
            var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("Foundry API response: {Response}", responseContent);

            return responseContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Foundry API");
            throw;
        }
    }

    private List<ExtractedField> ParseExtractionResponse(string response)
    {
        var fields = new List<ExtractedField>();
        try
        {
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            // Try to get the content from different response formats
            string? messageContent = null;

            // Azure AI Foundry format: output array
            // Structure: output[n] where type="message" has content[0].text
            if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
            {
                foreach (var outputItem in output.EnumerateArray())
                {
                    // Skip reasoning type, look for message type
                    if (outputItem.TryGetProperty("type", out var typeProperty) &&
                        typeProperty.GetString() == "message")
                    {
                        if (outputItem.TryGetProperty("content", out var content) &&
                            content.ValueKind == JsonValueKind.Array &&
                            content.GetArrayLength() > 0)
                        {
                            var firstContent = content[0];
                            if (firstContent.TryGetProperty("text", out var text))
                            {
                                messageContent = text.GetString(); break;
                            }
                        }
                    }
                }
            }
            // OpenAI format: choices[0].message.content
            else if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
            {
                messageContent = choices[0].GetProperty("message").GetProperty("content").GetString();
            }
            if (messageContent != null)
            {
                // Try to parse as JSON array of fields
                fields = ParseFieldsFromContent(messageContent);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing extraction response JSON");
        }

        return fields;
    }

    /// <summary>
    /// Parses extraction response and applies per-field confidence thresholds from metadata.
    /// </summary>
    private List<ExtractedField> ParseExtractionResponse(string response, List<FieldDefinition> fieldDefinitions)
    {
        // First, get base parsed fields
        var fields = ParseExtractionResponse(response);

        // Create lookup for field definitions
        var fieldDefLookup = fieldDefinitions.ToDictionary(
            f => f.FieldName,
            f => f,
            StringComparer.OrdinalIgnoreCase);

        // Apply per-field confidence thresholds and enrich with metadata
        foreach (var field in fields)
        {
            if (fieldDefLookup.TryGetValue(field.FieldName, out var definition))
            {
                // Mark as needing review if below per-field threshold
                var threshold = definition.ConfidenceThreshold;
                field.RequiresReview = field.ConfidenceScore < threshold;

                // Log if field is below its specific threshold
                if (field.RequiresReview)
                {
                    _logger.LogInformation(
                        "Field {FieldName} confidence {Confidence:P0} below threshold {Threshold:P0}",
                        field.FieldName, field.ConfidenceScore, threshold);
                }
            }
        }

        return fields;
    }

    private List<ExtractedField> ParseFieldsFromContent(string content)
    {
        var fields = new List<ExtractedField>();

        try
        {
            // Try to extract JSON from markdown code blocks if present
            var jsonContent = content;
            if (content.Contains("```json"))
            {
                var start = content.IndexOf("```json") + 7;
                var end = content.IndexOf("```", start);
                if (end > start)
                {
                    jsonContent = content.Substring(start, end - start).Trim();
                }
            }
            else if (content.Contains("```"))
            {
                var start = content.IndexOf("```") + 3;
                var end = content.IndexOf("```", start);
                if (end > start)
                {
                    jsonContent = content.Substring(start, end - start).Trim();
                }
            }

            using var contentDoc = JsonDocument.Parse(jsonContent);
            var contentRoot = contentDoc.RootElement;

            if (contentRoot.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in contentRoot.EnumerateArray())
                {
                    // Handle value that could be string or object
                    var extractedValue = string.Empty;
                    if (item.TryGetProperty("value", out var val))
                    {
                        extractedValue = val.ValueKind switch
                        {
                            JsonValueKind.String => val.GetString() ?? string.Empty,
                            JsonValueKind.Null => string.Empty,
                            _ => val.GetRawText() // Serialize object/array as JSON string
                        };
                    }

                    var field = new ExtractedField
                    {
                        Id = Guid.NewGuid(),
                        FieldName = item.TryGetProperty("fieldName", out var fn) ? fn.GetString() ?? string.Empty : string.Empty,
                        ExtractedValue = extractedValue,
                        ConfidenceScore = item.TryGetProperty("confidence", out var conf) ? GetDoubleValue(conf) : 0.5,
                        PageReference = item.TryGetProperty("pageRef", out var pageRef) ? GetStringValue(pageRef) : null,
                        ClauseReference = item.TryGetProperty("clauseRef", out var clauseRef) ? GetStringValue(clauseRef) : null,
                        RawExcerpt = item.TryGetProperty("excerpt", out var excerpt) ? GetStringValue(excerpt) : null,
                        ExtractedAt = DateTime.UtcNow,
                        FieldType = FieldType.Text,
                        RequiresReview = false // Set by ApplyFieldThresholds based on per-field ConfidenceThreshold
                    };
                    fields.Add(field);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse content as JSON fields array: {Content}", content);
        }

        return fields;
    }

    private static string? GetStringValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }

    private static double GetDoubleValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.String => double.TryParse(element.GetString(), out var d) ? d : 0.5,
            _ => 0.5
        };
    }

    private (ExtractedField, double) ParseValidationResponse(string fieldName, string extractedValue, string response)
    {
        var confidence = 0.5;
        var correctedValue = extractedValue;

        try
        {
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
            {
                var messageContent = choices[0].GetProperty("message").GetProperty("content").GetString();
                if (messageContent != null)
                {
                    using var contentDoc = JsonDocument.Parse(messageContent);
                    var contentRoot = contentDoc.RootElement;

                    if (contentRoot.TryGetProperty("confidence", out var confProp))
                    {
                        confidence = confProp.GetDouble();
                    }

                    if (contentRoot.TryGetProperty("correctedValue", out var correctedProp))
                    {
                        correctedValue = correctedProp.GetString() ?? extractedValue;
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing validation response");
        }

        var field = new ExtractedField
        {
            Id = Guid.NewGuid(),
            FieldName = fieldName,
            ExtractedValue = correctedValue,
            ConfidenceScore = confidence,
            ExtractedAt = DateTime.UtcNow,
            FieldType = FieldType.Text,
            RequiresReview = false // Validated/corrected fields don't need additional review
        };

        return (field, confidence);
    }

    private static string GetMockExtractionResponse()
    {
        // Return mock data that matches the expected JSON format for extraction
        var mockFields = new[]
        {
            new { fieldName = "TenantName", value = "ABC Corporation", confidence = 0.95, pageRef = "Page 1", clauseRef = "Section 1.1", excerpt = "This Lease Agreement is entered into by ABC Corporation..." },
            new { fieldName = "LandlordName", value = "XYZ Properties LLC", confidence = 0.92, pageRef = "Page 1", clauseRef = "Section 1.1", excerpt = "...and XYZ Properties LLC as Landlord..." },
            new { fieldName = "LeaseStartDate", value = "2024-01-01", confidence = 0.88, pageRef = "Page 2", clauseRef = "Section 2.1", excerpt = "The term shall commence on January 1, 2024..." },
            new { fieldName = "LeaseEndDate", value = "2029-12-31", confidence = 0.85, pageRef = "Page 2", clauseRef = "Section 2.1", excerpt = "...and terminate on December 31, 2029." },
            new { fieldName = "MonthlyBaseRent", value = "$15,000.00", confidence = 0.90, pageRef = "Page 3", clauseRef = "Section 3.1", excerpt = "Tenant shall pay base rent of Fifteen Thousand Dollars ($15,000.00) per month..." },
            new { fieldName = "SecurityDeposit", value = "$30,000.00", confidence = 0.87, pageRef = "Page 4", clauseRef = "Section 4.1", excerpt = "Security deposit equal to two months' rent: $30,000.00" },
            new { fieldName = "PremisesAddress", value = "123 Commercial Blvd, Suite 100, Business City, ST 12345", confidence = 0.93, pageRef = "Page 1", clauseRef = "Section 1.2", excerpt = "Premises located at 123 Commercial Blvd..." },
            new { fieldName = "SquareFootage", value = "5,000 sq ft", confidence = 0.65, pageRef = "Page 2", clauseRef = "Section 1.3", excerpt = "approximately 5,000 rentable square feet" },
            new { fieldName = "CAMCharges", value = "$3.50 per sq ft annually", confidence = 0.72, pageRef = "Page 5", clauseRef = "Section 5.2", excerpt = "Common Area Maintenance charges of $3.50 per square foot..." },
            new { fieldName = "RenewalOption", value = "Two 5-year renewal options", confidence = 0.78, pageRef = "Page 8", clauseRef = "Section 8.1", excerpt = "Tenant shall have two (2) options to renew for five (5) years each..." }
        };

        return JsonSerializer.Serialize(mockFields);
    }
}
