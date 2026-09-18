namespace LeaseDocumentIntelligence.Domain.Interfaces;

using LeaseDocumentIntelligence.Domain.Models;

/// <summary>
/// Repository for document metadata stored in Cosmos DB
/// </summary>
public interface IDocumentMetadataRepository
{
    /// <summary>
    /// Creates a new document metadata record
    /// </summary>
    Task<DocumentMetadata> CreateAsync(
        DocumentMetadata metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets document metadata by ID
    /// </summary>
    Task<DocumentMetadata?> GetByIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all documents for a specific user
    /// </summary>
    Task<IReadOnlyList<DocumentMetadata>> GetByUserAsync(
        string uploadedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all documents with a specific status
    /// </summary>
    Task<IReadOnlyList<DocumentMetadata>> GetByStatusAsync(
        DocumentStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates document metadata
    /// </summary>
    Task<DocumentMetadata> UpdateAsync(
        DocumentMetadata metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the status of a document
    /// </summary>
    Task UpdateStatusAsync(
        Guid documentId,
        DocumentStatus newStatus,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes document metadata
    /// </summary>
    Task DeleteAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all document metadata with pagination
    /// </summary>
    Task<IReadOnlyList<DocumentMetadata>> GetAllAsync(
        int pageSize = 100,
        string? continuationToken = null,
        CancellationToken cancellationToken = default);
}
