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
    Task<SearchResultDto> SearchAsync(string query, SearchOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs hybrid search (keyword + vector) with AI-powered reasoning.
    /// </summary>
    Task<ReasonedSearchResultDto> SearchWithReasoningAsync(string query, SearchOptions? options = null, CancellationToken cancellationToken = default);

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
/// Options for search queries.
/// </summary>
public class SearchOptions
{
    public int Top { get; set; } = 10;
    public bool IncludeExcerpts { get; set; } = true;
    public string? TenantFilter { get; set; }
    public double MinScore { get; set; } = 0.5;
    public bool UseHybridSearch { get; set; } = true;
}
