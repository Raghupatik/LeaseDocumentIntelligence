namespace LeaseDocumentIntelligence.Domain.Interfaces;

using LeaseDocumentIntelligence.Domain.Models;

/// <summary>
/// Abstraction for blob storage operations with batch upload support
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Uploads a single document to blob storage
    /// </summary>
    /// <param name="document">The lease document to upload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The blob URI where the document was stored</returns>
    Task<Uri> UploadDocumentAsync(
        LeaseDocument document,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads multiple documents in parallel with controlled concurrency
    /// </summary>
    /// <param name="documents">The documents to upload</param>
    /// <param name="maxConcurrency">Maximum concurrent uploads (default: 4)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Results of each upload operation</returns>
    Task<IReadOnlyList<BlobUploadResult>> UploadDocumentsBatchAsync(
        IEnumerable<LeaseDocument> documents,
        int maxConcurrency = 4,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a document from blob storage
    /// </summary>
    /// <param name="blobUri">The URI of the blob to download</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The document content as byte array</returns>
    Task<byte[]> DownloadDocumentAsync(
        Uri blobUri,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document from blob storage
    /// </summary>
    /// <param name="blobUri">The URI of the blob to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteDocumentAsync(
        Uri blobUri,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a blob upload operation
/// </summary>
public record BlobUploadResult(
    Guid DocumentId,
    bool IsSuccess,
    Uri? BlobUri,
    string? ErrorMessage);
