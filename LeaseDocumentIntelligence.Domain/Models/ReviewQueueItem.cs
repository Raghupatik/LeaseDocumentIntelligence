namespace LeaseDocumentIntelligence.Domain.Models;

public class ReviewQueueItem
{
    public Guid Id { get; set; }
    public Guid LeaseDocumentId { get; set; }
    public LeaseDocument? LeaseDocument { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string ExtractedValue { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string? PageReference { get; set; }
    public string? ClauseReference { get; set; }
    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ReviewedValue { get; set; }
    public string? ReviewNotes { get; set; }
}

public enum ReviewStatus
{
    Pending,
    Approved,
    Rejected,
    Corrected
}
