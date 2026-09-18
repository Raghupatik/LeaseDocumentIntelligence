namespace LeaseDocumentIntelligence.API.Controllers;

using LeaseDocumentIntelligence.Domain.DTOs;
using LeaseDocumentIntelligence.Domain.Interfaces;
using LeaseDocumentIntelligence.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AllAuthenticatedUsers")]
public class LeaseController : ControllerBase
{
    private readonly IDocumentProcessingService _documentProcessing;
    private readonly IExtractionService _extractionService;
    private readonly ILeaseDocumentRepository _repository;
    private readonly IBlobStorageService _blobStorage;
    private readonly IDocumentMetadataRepository _metadataRepository;
    private readonly ILogger<LeaseController> _logger;

    public LeaseController(
        IDocumentProcessingService documentProcessing,
        IExtractionService extractionService,
        ILeaseDocumentRepository repository,
        IBlobStorageService blobStorage,
        IDocumentMetadataRepository metadataRepository,
        ILogger<LeaseController> logger)
    {
        _documentProcessing = documentProcessing;
        _extractionService = extractionService;
        _repository = repository;
        _blobStorage = blobStorage;
        _metadataRepository = metadataRepository;
        _logger = logger;
    }

    /// <summary>
    /// Upload a lease document for extraction
    /// </summary>
    /// <param name="file">PDF lease document</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Document ID for tracking extraction status</returns>
    [HttpPost("upload")]
    [AllowAnonymous] // Allow Blazor UI to call without auth cookie forwarding
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<ActionResult<Guid>> UploadDocument(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("Upload attempted with empty file");
            return BadRequest("File is required");
        }

        try
        {
            var isValid = await _documentProcessing.ValidateDocumentAsync(
                file.FileName,
                file.ContentType,
                cancellationToken);

            if (!isValid)
            {
                _logger.LogWarning("Invalid document format: {FileName}", file.FileName);
                return StatusCode(StatusCodes.Status415UnsupportedMediaType, "Only PDF files are supported");
            }

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);

            var uploadedBy = User?.Identity?.Name ?? "Anonymous";
            var documentId = Guid.NewGuid();
            var uploadedAt = DateTime.UtcNow;

            var document = new LeaseDocument
            {
                Id = documentId,
                FileName = file.FileName,
                FileContent = memoryStream.ToArray(),
                ContentType = file.ContentType,
                FileSize = file.Length,
                UploadedBy = uploadedBy,
                UploadedAt = uploadedAt,
                Status = DocumentStatus.Pending
            };

            // Upload to Blob Storage
            var blobUri = await _blobStorage.UploadDocumentAsync(document, cancellationToken);
            // Store metadata in Cosmos DB
            var metadata = new DocumentMetadata
            {
                DocumentId = documentId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileSize = file.Length,
                BlobUri = blobUri.ToString(),
                UploadedBy = uploadedBy,
                UploadedAt = uploadedAt,
                Status = DocumentStatus.Processing
            };

            await _metadataRepository.CreateAsync(metadata, cancellationToken);
            // Also store in legacy repository for extraction service compatibility
            var createdDocument = await _repository.CreateAsync(document, cancellationToken);

            // Wait for extraction to complete
            var result = await _extractionService.ExtractLeaseDataAsync(createdDocument, cancellationToken);
            // Update metadata with extraction results
            metadata.Status = DocumentStatus.Completed;
            metadata.ExtractedFieldCount = result.Fields.Count;
            metadata.ReviewRequiredCount = result.ReviewQueueItems.Count;
            await _metadataRepository.UpdateAsync(metadata, cancellationToken);

            return Ok(documentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document");
            return StatusCode(StatusCodes.Status500InternalServerError, "Error processing document");
        }
    }

    /// <summary>
    /// Batch upload multiple lease documents with parallel processing
    /// </summary>
    /// <param name="files">PDF lease documents</param>
    /// <param name="maxConcurrency">Maximum concurrent uploads (default: 4)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Results of each upload operation</returns>
    [HttpPost("upload/batch")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchUploadResultDto>> UploadDocumentsBatch(
        [FromForm] List<IFormFile> files,
        [FromQuery] int maxConcurrency = 4,
        CancellationToken cancellationToken = default)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest("At least one file is required");
        }

        var uploadedBy = User?.Identity?.Name ?? "Anonymous";
        var documents = new List<LeaseDocument>();

        // Prepare documents
        foreach (var file in files)
        {
            var isValid = await _documentProcessing.ValidateDocumentAsync(
                file.FileName, file.ContentType, cancellationToken);

            if (!isValid)
            {
                _logger.LogWarning("Skipping invalid file in batch: {FileName}", file.FileName);
                continue;
            }

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);

            documents.Add(new LeaseDocument
            {
                Id = Guid.NewGuid(),
                FileName = file.FileName,
                FileContent = memoryStream.ToArray(),
                ContentType = file.ContentType,
                FileSize = file.Length,
                UploadedBy = uploadedBy,
                UploadedAt = DateTime.UtcNow,
                Status = DocumentStatus.Pending
            });
        }

        if (documents.Count == 0)
        {
            return BadRequest("No valid PDF files found in the batch");
        }
        // Batch upload to blob storage with controlled concurrency
        var blobResults = await _blobStorage.UploadDocumentsBatchAsync(
            documents, maxConcurrency, cancellationToken);

        // Store metadata for successful uploads
        var results = new List<BatchUploadItemResultDto>();
        foreach (var blobResult in blobResults)
        {
            var doc = documents.First(d => d.Id == blobResult.DocumentId);

            if (blobResult.IsSuccess && blobResult.BlobUri != null)
            {
                var metadata = new DocumentMetadata
                {
                    DocumentId = blobResult.DocumentId,
                    FileName = doc.FileName,
                    ContentType = doc.ContentType,
                    FileSize = doc.FileSize,
                    BlobUri = blobResult.BlobUri.ToString(),
                    UploadedBy = uploadedBy,
                    UploadedAt = doc.UploadedAt,
                    Status = DocumentStatus.Pending
                };

                await _metadataRepository.CreateAsync(metadata, cancellationToken);
            }

            results.Add(new BatchUploadItemResultDto(
                blobResult.DocumentId,
                doc.FileName,
                blobResult.IsSuccess,
                blobResult.ErrorMessage));
        }

        var successCount = results.Count(r => r.IsSuccess);
        return Ok(new BatchUploadResultDto(results.Count, successCount, results));
    }

    /// <summary>
    /// Get extraction results for a document
    /// </summary>
    /// <param name="documentId">Document ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extraction results with confidence scores and citations</returns>
    [HttpGet("{documentId}/results")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExtractionResultDto>> GetExtractionResults(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _extractionService.GetExtractionResultAsync(documentId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Document not found: {DocumentId}", documentId);
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving extraction results");
            return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving results");
        }
    }

    /// <summary>
    /// Get document status
    /// </summary>
    /// <param name="documentId">Document ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Document status</returns>
    [HttpGet("{documentId}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> GetDocumentStatus(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var document = await _repository.GetByIdAsync(documentId, cancellationToken);
            if (document == null)
            {
                return NotFound($"Document {documentId} not found");
            }

            return Ok(new
            {
                documentId = document.Id,
                fileName = document.FileName,
                status = document.Status.ToString(),
                uploadedAt = document.UploadedAt,
                extractedFieldCount = document.ExtractedFields.Count,
                reviewQueueCount = document.ReviewQueueItems.Count(r => r.Status == Domain.Models.ReviewStatus.Pending)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document status");
            return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving status");
        }
    }

    /// <summary>
    /// List all documents
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of documents</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<object>>> ListDocuments(
        CancellationToken cancellationToken)
    {
        try
        {
            var documents = await _repository.GetAllAsync(cancellationToken);
            var response = documents.Select(d => new
            {
                d.Id,
                d.FileName,
                Status = d.Status.ToString(),
                d.UploadedAt,
                FieldCount = d.ExtractedFields.Count,
                ReviewCount = d.ReviewQueueItems.Count(r => r.Status == Domain.Models.ReviewStatus.Pending)
            }).ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents");
            return StatusCode(StatusCodes.Status500InternalServerError, "Error listing documents");
        }
    }
}
