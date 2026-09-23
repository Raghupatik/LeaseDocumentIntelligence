using LeaseDocumentIntelligence.Domain.Interfaces;

namespace LeaseDocumentIntelligence.Infrastructure.Services;

using LeaseDocumentIntelligence.Domain.DTOs;
using LeaseDocumentIntelligence.Domain.Models;
using Microsoft.Extensions.Logging;

public class ReviewQueueService : IReviewQueueService
{
    private readonly ILeaseDocumentRepository _repository;
    private readonly IDocumentMetadataRepository _metadataRepository;
    private readonly ILogger<ReviewQueueService> _logger;
    private static readonly List<ReviewQueueItem> _reviewQueue = new();

    public ReviewQueueService(
        ILeaseDocumentRepository repository,
        IDocumentMetadataRepository metadataRepository,
        ILogger<ReviewQueueService> logger)
    {
        _repository = repository;
        _metadataRepository = metadataRepository;
        _logger = logger;
    }

    public async Task AddToReviewQueueAsync(
        Guid documentId,
        List<ExtractedField> fieldsForReview,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await _repository.GetByIdAsync(documentId, cancellationToken);
            if (document == null)
            {
                throw new InvalidOperationException($"Document {documentId} not found");
            }

            foreach (var field in fieldsForReview)
            {
                var reviewItem = new ReviewQueueItem
                {
                    Id = Guid.NewGuid(),
                    LeaseDocumentId = documentId,
                    FieldName = field.FieldName,
                    ExtractedValue = field.ExtractedValue,
                    ConfidenceScore = field.ConfidenceScore,
                    PageReference = field.PageReference,
                    ClauseReference = field.ClauseReference,
                    Status = ReviewStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                document.ReviewQueueItems.Add(reviewItem);
                _reviewQueue.Add(reviewItem);
            }

            await _repository.UpdateAsync(document, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding fields to review queue");
            throw;
        }
    }

    public async Task<List<ReviewQueueItemDto>> GetReviewQueueAsync(
        CancellationToken cancellationToken = default)
    {
        var items = new List<ReviewQueueItemDto>();

        try
        {
            // Get all documents that need review from Cosmos DB
            var documents = await _metadataRepository.GetByStatusAsync(DocumentStatus.ReviewRequired, cancellationToken);

            foreach (var doc in documents)
            {
                if (doc.ExtractedFields == null) continue;

                // Get fields that require review based on per-field ConfidenceThreshold
                var reviewFields = doc.ExtractedFields
                    .Where(f => f.RequiresReview)
                    .Select(f => new ReviewQueueItemDto
                    {
                        Id = f.Id != Guid.Empty ? f.Id : Guid.NewGuid(),
                        DocumentId = doc.DocumentId,
                        FileName = doc.FileName ?? "Unknown",
                        FieldName = f.FieldName,
                        ExtractedValue = f.ExtractedValue ?? "",
                        ConfidenceScore = f.ConfidenceScore,
                        PageReference = f.PageReference,
                        ClauseReference = f.ClauseReference,
                        Status = "Pending"
                    });

                items.AddRange(reviewFields);
            }

            _logger.LogInformation("Retrieved {Count} review items from Cosmos", items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving review queue from Cosmos, falling back to in-memory");

            // Fallback to in-memory queue
            items = _reviewQueue
                .Where(r => r.Status == ReviewStatus.Pending)
                .Select(r => new ReviewQueueItemDto
                {
                    Id = r.Id,
                    FieldName = r.FieldName,
                    ExtractedValue = r.ExtractedValue,
                    ConfidenceScore = r.ConfidenceScore,
                    PageReference = r.PageReference,
                    ClauseReference = r.ClauseReference,
                    Status = r.Status.ToString()
                })
                .ToList();
        }

        return items;
    }

    public Task ApproveReviewItemAsync(
        Guid reviewItemId,
        CancellationToken cancellationToken = default)
    {
        var item = _reviewQueue.FirstOrDefault(r => r.Id == reviewItemId);
        if (item != null)
        {
            item.Status = ReviewStatus.Approved;
            item.ReviewedAt = DateTime.UtcNow;
        }
        else
        {
            _logger.LogWarning("Review item {ReviewItemId} not found", reviewItemId);
        }
        return Task.CompletedTask;
    }

    public Task RejectReviewItemAsync(
        Guid reviewItemId,
        string correctedValue,
        string notes,
        CancellationToken cancellationToken = default)
    {
        var item = _reviewQueue.FirstOrDefault(r => r.Id == reviewItemId);
        if (item != null)
        {
            item.Status = ReviewStatus.Corrected;
            item.ReviewedValue = correctedValue;
            item.ReviewNotes = notes;
            item.ReviewedAt = DateTime.UtcNow;
        }
        else
        {
            _logger.LogWarning("Review item {ReviewItemId} not found", reviewItemId);
        }
        return Task.CompletedTask;
    }
}
