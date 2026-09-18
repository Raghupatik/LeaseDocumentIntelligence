namespace LeaseDocumentIntelligence.Domain.Interfaces;

using LeaseDocumentIntelligence.Domain.DTOs;
using LeaseDocumentIntelligence.Domain.Models;

public interface IReviewQueueService
{
    Task AddToReviewQueueAsync(
        Guid documentId,
        List<ExtractedField> fieldsForReview,
        CancellationToken cancellationToken = default);

    Task<List<ReviewQueueItemDto>> GetReviewQueueAsync(
        CancellationToken cancellationToken = default);

    Task ApproveReviewItemAsync(
        Guid reviewItemId,
        CancellationToken cancellationToken = default);

    Task RejectReviewItemAsync(
        Guid reviewItemId,
        string correctedValue,
        string notes,
        CancellationToken cancellationToken = default);
}
