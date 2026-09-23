namespace LeaseDocumentIntelligence.Domain.Interfaces;

using LeaseDocumentIntelligence.Domain.DTOs;
using LeaseDocumentIntelligence.Domain.Models;

/// <summary>
/// Service for Azure AI Search operations with vector embeddings.
/// </summary>
public interface IVectorSearchService
{
    /// <summary>
    /// Indexes a lease document with its extracted fields and embeddings.
    /// </summary>
    Task IndexDocumentAsync(LeaseDocument document, string documentText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for similar documents using semantic/vector search.
    /// </summary>
    Task<SearchResultDto> SearchAsync(string query, LeaseSearchOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs hybrid search (keyword + vector) with AI-powered reasoning.
    /// </summary>
    Task<ReasonedSearchResultDto> SearchWithReasoningAsync(string query, LeaseSearchOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document from the search index.
    /// </summary>
    Task DeleteDocumentAsync(string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the search index exists with proper schema.
    /// </summary>
    Task EnsureIndexExistsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for lease search queries.
/// </summary>
public class LeaseSearchOptions
{
    public int Top { get; set; } = 10;
    public bool IncludeExcerpts { get; set; } = true;
    public string? TenantFilter { get; set; }
    /// <summary>
    /// Minimum relevance score (0.0-1.0). Lower values return more results.
    /// Default 0.01 to include most matches - AI reasoning will filter irrelevant ones.
    /// </summary>
    public double MinScore { get; set; } = 0.01;
    public bool UseHybridSearch { get; set; } = true;
}
