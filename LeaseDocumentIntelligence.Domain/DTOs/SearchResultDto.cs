namespace LeaseDocumentIntelligence.Domain.DTOs;

/// <summary>
/// Search result containing matched documents.
/// </summary>
public class SearchResultDto
{
    public string Query { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public List<SearchHitDto> Hits { get; set; } = [];
    public double SearchTimeMs { get; set; }
}

/// <summary>
/// Individual search hit with relevance score.
/// </summary>
public class SearchHitDto
{
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
    public double Score { get; set; }
    public List<string> Highlights { get; set; } = [];
    public Dictionary<string, string> ExtractedFields { get; set; } = [];
}

/// <summary>
/// Search result with AI-generated reasoning and insights.
/// </summary>
public class ReasonedSearchResultDto
{
    public string Query { get; set; } = string.Empty;
    public SearchResultDto SearchResults { get; set; } = new();

    /// <summary>
    /// AI-generated summary explaining the search results.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// AI reasoning about why these documents match the query.
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;

    /// <summary>
    /// Key insights extracted from the matching documents.
    /// </summary>
    public List<string> KeyInsights { get; set; } = [];

    /// <summary>
    /// Suggested follow-up questions based on results.
    /// </summary>
    public List<string> SuggestedQuestions { get; set; } = [];

    /// <summary>
    /// Citations linking insights to source documents.
    /// </summary>
    public List<CitationDto> Citations { get; set; } = [];
}

/// <summary>
/// Citation linking an insight to its source.
/// </summary>
public class CitationDto
{
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public int? PageNumber { get; set; }
}

/// <summary>
/// Request to search leases with natural language.
/// </summary>
public class SearchRequestDto
{
    public required string Query { get; set; }
    public int Top { get; set; } = 10;
    public bool IncludeReasoning { get; set; } = true;
    public string? TenantFilter { get; set; }
}
