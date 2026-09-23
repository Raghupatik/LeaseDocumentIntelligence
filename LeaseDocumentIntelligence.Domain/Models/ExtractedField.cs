namespace LeaseDocumentIntelligence.Domain.Models;

using System.Text.Json.Serialization;

public class ExtractedField
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("leaseDocumentId")]
    public Guid LeaseDocumentId { get; set; }

    [JsonIgnore]
    public LeaseDocument? LeaseDocument { get; set; }

    [JsonPropertyName("fieldName")]
    public string FieldName { get; set; } = string.Empty;

    [JsonPropertyName("extractedValue")]
    public string ExtractedValue { get; set; } = string.Empty;

    [JsonPropertyName("confidenceScore")]
    public double ConfidenceScore { get; set; }

    [JsonPropertyName("pageReference")]
    public string? PageReference { get; set; }

    [JsonPropertyName("clauseReference")]
    public string? ClauseReference { get; set; }

    [JsonPropertyName("rawExcerpt")]
    public string? RawExcerpt { get; set; }

    [JsonPropertyName("extractedAt")]
    public DateTime ExtractedAt { get; set; }

    [JsonPropertyName("fieldType")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public FieldType FieldType { get; set; } = FieldType.Text;

    [JsonPropertyName("requiresReview")]
    public bool RequiresReview { get; set; }
}

public enum FieldType
{
    Currency,
    Date,
    Percentage,
    Text,
    Option,
    Duration,
    Complex
}
