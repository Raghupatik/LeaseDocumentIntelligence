namespace LeaseDocumentIntelligence.Domain.Models;

public class LeaseDocument
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public byte[] FileContent { get; set; } = [];
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
    public ICollection<ExtractedField> ExtractedFields { get; set; } = new List<ExtractedField>();
    public ICollection<ReviewQueueItem> ReviewQueueItems { get; set; } = new List<ReviewQueueItem>();
}

public enum DocumentStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    ReviewRequired
}
