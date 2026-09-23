namespace LeaseDocumentIntelligence.Domain.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Document metadata stored in Cosmos DB
/// </summary>
public class DocumentMetadata
{
    /// <summary>
    /// Unique identifier for the document (partition key)
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Document ID as GUID
    /// </summary>
    [JsonPropertyName("documentId")]
    public Guid DocumentId { get; set; }

    /// <summary>
    /// Original file name
    /// </summary>
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Content type (e.g., application/pdf)
    /// </summary>
    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    /// <summary>
    /// URI of the document in blob storage
    /// </summary>
    [JsonPropertyName("blobUri")]
    public string BlobUri { get; set; } = string.Empty;

    /// <summary>
    /// Username of the person who uploaded the document
    /// </summary>
    [JsonPropertyName("uploadedBy")]
    public string UploadedBy { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the document was uploaded
    /// </summary>
    [JsonPropertyName("uploadedAt")]
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// Current processing status
    /// </summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    /// <summary>
    /// Timestamp of last status update
    /// </summary>
    [JsonPropertyName("lastUpdatedAt")]
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of fields extracted (populated after extraction)
    /// </summary>
    [JsonPropertyName("extractedFieldCount")]
    public int ExtractedFieldCount { get; set; }

    /// <summary>
    /// Number of fields requiring review
    /// </summary>
    [JsonPropertyName("reviewRequiredCount")]
    public int ReviewRequiredCount { get; set; }

    /// <summary>
    /// Extracted fields from the document
    /// </summary>
    [JsonPropertyName("extractedFields")]
    public List<ExtractedField> ExtractedFields { get; set; } = new();

    /// <summary>
    /// Partition key for Cosmos DB (using documentId for point reads)
    /// </summary>
    public string Pk { get; set; } = string.Empty;
}
