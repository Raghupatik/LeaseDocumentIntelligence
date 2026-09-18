namespace LeaseDocumentIntelligence.Domain.DTOs;

public class ExtractionResultDto
{
    public Guid DocumentId { get; set; }
    public string DocumentFileName { get; set; } = string.Empty;
    public DateTime ExtractedAt { get; set; }
    public List<ExtractedFieldDto> Fields { get; set; } = new();
    public List<ReviewQueueItemDto> ReviewQueueItems { get; set; } = new();
    public ExtractionSummaryDto Summary { get; set; } = new();
}

public class ExtractedFieldDto
{
    public Guid Id { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string ExtractedValue { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string? PageReference { get; set; }
    public string? ClauseReference { get; set; }
    public string? RawExcerpt { get; set; }
    public string FieldType { get; set; } = string.Empty;
    public bool RequiresReview { get; set; }
}

public class ReviewQueueItemDto
{
    public Guid Id { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string ExtractedValue { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string? PageReference { get; set; }
    public string? ClauseReference { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ExtractionSummaryDto
{
    public int TotalFieldsExtracted { get; set; }
    public int HighConfidenceFields { get; set; }
    public int LowConfidenceFields { get; set; }
    public double AverageConfidenceScore { get; set; }
    public int ItemsRequiringReview { get; set; }
}
