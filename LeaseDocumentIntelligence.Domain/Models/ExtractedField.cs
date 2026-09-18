namespace LeaseDocumentIntelligence.Domain.Models;

public class ExtractedField
{
    public Guid Id { get; set; }
    public Guid LeaseDocumentId { get; set; }
    public LeaseDocument? LeaseDocument { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string ExtractedValue { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string? PageReference { get; set; }
    public string? ClauseReference { get; set; }
    public string? RawExcerpt { get; set; }
    public DateTime ExtractedAt { get; set; }
    public FieldType FieldType { get; set; }
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
