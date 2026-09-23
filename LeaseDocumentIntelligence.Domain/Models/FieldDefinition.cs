namespace LeaseDocumentIntelligence.Domain.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a configurable field definition for lease extraction.
/// Stored in Cosmos DB for runtime configuration without deployment.
/// </summary>
public class FieldDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("pk")]
    public string Pk { get; set; } = "FieldDefinition";

    [JsonPropertyName("fieldName")]
    public string FieldName { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("fieldType")]
    public FieldDataType FieldType { get; set; } = FieldDataType.Text;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("isRequired")]
    public bool IsRequired { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("confidenceThreshold")]
    public double ConfidenceThreshold { get; set; } = 0.7;

    [JsonPropertyName("extractionHint")]
    public string ExtractionHint { get; set; } = string.Empty;

    [JsonPropertyName("validationRegex")]
    public string? ValidationRegex { get; set; }

    [JsonPropertyName("displayOrder")]
    public int DisplayOrder { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    [JsonPropertyName("updatedBy")]
    public string UpdatedBy { get; set; } = string.Empty;
}

public enum FieldDataType
{
    Text,
    Currency,
    Date,
    Percentage,
    Duration,
    Boolean,
    Complex,
    Option
}
