using LeaseDocumentIntelligence.Domain.Interfaces;

namespace LeaseDocumentIntelligence.Infrastructure.Services;

using LeaseDocumentIntelligence.Domain.DTOs;
using LeaseDocumentIntelligence.Domain.Models;
using Microsoft.Extensions.Logging;

public class ReviewQueueService : IReviewQueueService
{
    private readonly ILeaseDocumentRepository _repository;
    private readonly ILogger<ReviewQueueService> _logger;
    private static readonly List<ReviewQueueItem> _reviewQueue = new();

    public ReviewQueueService(
        ILeaseDocumentRepository repository,
        ILogger<ReviewQueueService> logger)
    {
        _repository = repository;
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

    public Task<List<ReviewQueueItemDto>> GetReviewQueueAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var items = _reviewQueue
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

            return items;
        }, cancellationToken);
    }

    public Task ApproveReviewItemAsync(
        Guid reviewItemId,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
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
        }, cancellationToken);
    }

    public Task RejectReviewItemAsync(
        Guid reviewItemId,
        string correctedValue,
        string notes,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
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
        }, cancellationToken);
    }
}
