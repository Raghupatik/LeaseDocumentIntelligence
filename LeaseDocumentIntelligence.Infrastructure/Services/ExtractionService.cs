using LeaseDocumentIntelligence.Domain.Interfaces;

namespace LeaseDocumentIntelligence.Infrastructure.Services;

using LeaseDocumentIntelligence.Domain.Constants;
using LeaseDocumentIntelligence.Domain.DTOs;
using LeaseDocumentIntelligence.Domain.Models;
using Microsoft.Extensions.Logging;

public class ExtractionService : IExtractionService
{
    private readonly IDocumentProcessingService _documentProcessing;
    private readonly IFoundryAIService _foundryAI;
    private readonly IReviewQueueService _reviewQueue;
    private readonly ILeaseDocumentRepository _repository;
    private readonly ILogger<ExtractionService> _logger;

    public ExtractionService(
        IDocumentProcessingService documentProcessing,
        IFoundryAIService foundryAI,
        IReviewQueueService reviewQueue,
        ILeaseDocumentRepository repository,
        ILogger<ExtractionService> logger)
    {
        _documentProcessing = documentProcessing;
        _foundryAI = foundryAI;
        _reviewQueue = reviewQueue;
        _repository = repository;
        _logger = logger;
    }

    public async Task<ExtractionResultDto> ExtractLeaseDataAsync(
        LeaseDocument document,
        CancellationToken cancellationToken = default)
    {
        try
        {
            document.Status = DocumentStatus.Processing;
            await _repository.UpdateAsync(document, cancellationToken);            var documentText = await _documentProcessing.ExtractTextFromPdfAsync(
                document.FileContent,
                cancellationToken);            var fieldDefs = LeaseFieldDefinitions.Fields.ToDictionary(
                f => f.Key,
                f => f.Value.Name);

            var extractedFields = await _foundryAI.ExtractFieldsAsync(
                documentText,
                fieldDefs,
                cancellationToken);

            foreach (var field in extractedFields)
            {
                field.LeaseDocumentId = document.Id;
            }

            document.ExtractedFields = extractedFields;

            var fieldsForReview = extractedFields
                .Where(f => f.ConfidenceScore < 0.7)
                .ToList();

            if (fieldsForReview.Count > 0)
            {
                await _reviewQueue.AddToReviewQueueAsync(document.Id, fieldsForReview, cancellationToken);
                document.Status = DocumentStatus.ReviewRequired;
            }
            else
            {
                document.Status = DocumentStatus.Completed;
            }

            await _repository.UpdateAsync(document, cancellationToken);
            return await GetExtractionResultAsync(document.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting lease data for document {DocumentId}", document.Id);
            document.Status = DocumentStatus.Failed;
            await _repository.UpdateAsync(document, cancellationToken);
            throw;
        }
    }

    public async Task<ExtractionResultDto> GetExtractionResultAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetByIdAsync(documentId, cancellationToken);
        if (document == null)
        {
            throw new InvalidOperationException($"Document {documentId} not found");
        }

        var fields = document.ExtractedFields.Select(f => new ExtractedFieldDto
        {
            Id = f.Id,
            FieldName = f.FieldName,
            ExtractedValue = f.ExtractedValue,
            ConfidenceScore = f.ConfidenceScore,
            PageReference = f.PageReference,
            ClauseReference = f.ClauseReference,
            RawExcerpt = f.RawExcerpt,
            FieldType = f.FieldType.ToString(),
            RequiresReview = f.RequiresReview
        }).ToList();

        var reviewItems = document.ReviewQueueItems.Select(r => new ReviewQueueItemDto
        {
            Id = r.Id,
            FieldName = r.FieldName,
            ExtractedValue = r.ExtractedValue,
            ConfidenceScore = r.ConfidenceScore,
            PageReference = r.PageReference,
            ClauseReference = r.ClauseReference,
            Status = r.Status.ToString()
        }).ToList();

        var summary = new ExtractionSummaryDto
        {
            TotalFieldsExtracted = fields.Count,
            HighConfidenceFields = fields.Count(f => f.ConfidenceScore >= 0.7),
            LowConfidenceFields = fields.Count(f => f.ConfidenceScore < 0.7),
            AverageConfidenceScore = fields.Count > 0 ? fields.Average(f => f.ConfidenceScore) : 0,
            ItemsRequiringReview = reviewItems.Count(r => r.Status == "Pending")
        };

        return new ExtractionResultDto
        {
            DocumentId = document.Id,
            DocumentFileName = document.FileName,
            ExtractedAt = DateTime.UtcNow,
            Fields = fields,
            ReviewQueueItems = reviewItems,
            Summary = summary
        };
    }
}
