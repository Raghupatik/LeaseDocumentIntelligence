namespace LeaseDocumentIntelligence.Infrastructure.Services;

using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using LeaseDocumentIntelligence.Domain.Interfaces;
using LeaseDocumentIntelligence.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

/// <summary>
/// Azure Blob Storage implementation with batch upload support
/// </summary>
public sealed class BlobStorageService : IBlobStorageService, IAsyncDisposable
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<BlobStorageService> _logger;
    private readonly BlobStorageOptions _options;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);
    private bool _containerInitialized;
    private bool _disposed;

    public BlobStorageService(
        IOptions<BlobStorageOptions> options,
        ILogger<BlobStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var credential = new DefaultAzureCredential();
        _blobServiceClient = new BlobServiceClient(
            new Uri(_options.StorageAccountUri),
            credential);

        _containerClient = _blobServiceClient.GetBlobContainerClient(_options.ContainerName);
    }

    public async Task<Uri> UploadDocumentAsync(
        LeaseDocument document,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await EnsureContainerExistsAsync(cancellationToken);

        var blobName = GenerateBlobName(document);
        var blobClient = _containerClient.GetBlobClient(blobName);
        var headers = new BlobHttpHeaders
        {
            ContentType = document.ContentType
        };

        var metadata = new Dictionary<string, string>
        {
            ["documentId"] = document.Id.ToString(),
            ["fileName"] = document.FileName,
            ["uploadedBy"] = document.UploadedBy,
            ["uploadedAt"] = document.UploadedAt.ToString("O")
        };

        await using var stream = new MemoryStream(document.FileContent);
        await blobClient.UploadAsync(
            stream,
            new BlobUploadOptions
            {
                HttpHeaders = headers,
                Metadata = metadata,
                TransferOptions = new Azure.Storage.StorageTransferOptions
                {
                    MaximumConcurrency = 4,
                    MaximumTransferSize = 50 * 1024 * 1024 // 50 MB chunks
                }
            },
            cancellationToken);
        return blobClient.Uri;
    }

    public async Task<IReadOnlyList<BlobUploadResult>> UploadDocumentsBatchAsync(
        IEnumerable<LeaseDocument> documents,
        int maxConcurrency = 4,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await EnsureContainerExistsAsync(cancellationToken);

        var results = new ConcurrentBag<BlobUploadResult>();
        var documentList = documents.ToList();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrency,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(documentList, parallelOptions, async (document, ct) =>
        {
            try
            {
                var uri = await UploadDocumentAsync(document, ct);
                results.Add(new BlobUploadResult(document.Id, true, uri, null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to upload document {DocumentId} in batch",
                    document.Id);
                results.Add(new BlobUploadResult(document.Id, false, null, ex.Message));
            }
        });

        var resultList = results.ToList();
        var successCount = resultList.Count(r => r.IsSuccess);
        return resultList;
    }

    public async Task<byte[]> DownloadDocumentAsync(
        Uri blobUri,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var blobClient = new BlobClient(blobUri, new DefaultAzureCredential());
        var response = await blobClient.DownloadContentAsync(cancellationToken);
        return response.Value.Content.ToArray();
    }

    public async Task DeleteDocumentAsync(
        Uri blobUri,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var blobClient = new BlobClient(blobUri, new DefaultAzureCredential());
        await blobClient.DeleteIfExistsAsync(
            DeleteSnapshotsOption.IncludeSnapshots,
            cancellationToken: cancellationToken);
    }

    private async Task EnsureContainerExistsAsync(CancellationToken cancellationToken)
    {
        if (_containerInitialized) return;

        await _initSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_containerInitialized) return;

            await _containerClient.CreateIfNotExistsAsync(
                PublicAccessType.None,
                cancellationToken: cancellationToken);

            _containerInitialized = true;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    private static string GenerateBlobName(LeaseDocument document)
    {
        var timestamp = document.UploadedAt.ToString("yyyyMMdd-HHmmss");
        var safeFileName = Path.GetFileNameWithoutExtension(document.FileName)
            .Replace(" ", "-")
            .ToLowerInvariant();
        var extension = Path.GetExtension(document.FileName);

        return $"{document.UploadedBy}/{timestamp}-{document.Id:N}-{safeFileName}{extension}";
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _initSemaphore.Dispose();
        _disposed = true;
        await Task.CompletedTask;
    }
}

/// <summary>
/// Configuration options for Blob Storage
/// </summary>
public class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    /// <summary>
    /// Storage account URI (e.g., https://mystorageaccount.blob.core.windows.net/)
    /// </summary>
    public string StorageAccountUri { get; set; } = string.Empty;

    /// <summary>
    /// Container name for lease documents
    /// </summary>
    public string ContainerName { get; set; } = "lease-documents";
}
