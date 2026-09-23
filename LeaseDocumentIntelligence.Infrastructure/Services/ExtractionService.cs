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
    private readonly IDocumentMetadataRepository _metadataRepository;
    private readonly IFieldDefinitionRepository _fieldDefinitionRepository;
    private readonly IVectorSearchService _searchService;
    private readonly ILogger<ExtractionService> _logger;

    public ExtractionService(
        IDocumentProcessingService documentProcessing,
        IFoundryAIService foundryAI,
        IReviewQueueService reviewQueue,
        ILeaseDocumentRepository repository,
        IDocumentMetadataRepository metadataRepository,
        IFieldDefinitionRepository fieldDefinitionRepository,
        IVectorSearchService searchService,
        ILogger<ExtractionService> logger)
    {
        _documentProcessing = documentProcessing;
        _foundryAI = foundryAI;
        _reviewQueue = reviewQueue;
        _repository = repository;
        _metadataRepository = metadataRepository;
        _fieldDefinitionRepository = fieldDefinitionRepository;
        _searchService = searchService;
        _logger = logger;
    }

    public async Task<ExtractionResultDto> ExtractLeaseDataAsync(
        LeaseDocument document,
        CancellationToken cancellationToken = default)
    {
        try
        {
            document.Status = DocumentStatus.Processing;
            await _repository.UpdateAsync(document, cancellationToken);
            var documentText = await _documentProcessing.ExtractTextFromPdfAsync(
                document.FileContent,
                cancellationToken);

            // Load active field definitions from metadata store
            var fieldDefinitions = await _fieldDefinitionRepository.GetActiveFieldsAsync(cancellationToken);

            List<ExtractedField> extractedFields;
            if (fieldDefinitions.Count > 0)
            {
                // Use metadata-driven extraction with per-field thresholds
                _logger.LogInformation("Using {Count} metadata-driven field definitions", fieldDefinitions.Count);
                extractedFields = await _foundryAI.ExtractFieldsAsync(
                    documentText,
                    fieldDefinitions,
                    cancellationToken);
            }
            else
            {
                // Fallback to hardcoded fields if no metadata configured
                _logger.LogWarning("No field definitions found, using hardcoded defaults");
                var fieldDefs = LeaseFieldDefinitions.Fields.ToDictionary(
                    f => f.Key,
                    f => f.Value.Name);
                extractedFields = await _foundryAI.ExtractFieldsAsync(
                    documentText,
                    fieldDefs,
                    cancellationToken);
            }

            foreach (var field in extractedFields)
            {
                field.LeaseDocumentId = document.Id;
            }

            document.ExtractedFields = extractedFields;

            // Fields marked RequiresReview by FoundryAI based on per-field ConfidenceThreshold
            var fieldsForReview = extractedFields
                .Where(f => f.RequiresReview)
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

            // Index document in Azure AI Search for semantic search
            try
            {
                await _searchService.IndexDocumentAsync(document, documentText, cancellationToken);
                _logger.LogInformation("Indexed document {DocumentId} in vector search", document.Id);
            }
            catch (Exception searchEx)
            {
                // Log but don't fail the extraction if indexing fails
                _logger.LogWarning(searchEx, "Failed to index document {DocumentId} in search, will retry later", document.Id);
            }

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
        // Try Cosmos metadata first (persistent storage)
        var metadata = await _metadataRepository.GetByIdAsync(documentId, cancellationToken);
        if (metadata != null)
        {
            var fields = metadata.ExtractedFields.Select(f => new ExtractedFieldDto
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

            var summary = new ExtractionSummaryDto
            {
                TotalFieldsExtracted = fields.Count,
                HighConfidenceFields = fields.Count(f => f.ConfidenceScore >= 0.7),
                LowConfidenceFields = fields.Count(f => f.ConfidenceScore < 0.7),
                AverageConfidenceScore = fields.Count > 0 ? fields.Average(f => f.ConfidenceScore) : 0,
                ItemsRequiringReview = metadata.ReviewRequiredCount
            };

            return new ExtractionResultDto
            {
                DocumentId = metadata.DocumentId,
                DocumentFileName = metadata.FileName,
                ExtractedAt = metadata.UploadedAt,
                Fields = fields,
                ReviewQueueItems = new List<ReviewQueueItemDto>(),
                Summary = summary
            };
        }

        // Fallback to in-memory repository for backwards compatibility during extraction
        var document = await _repository.GetByIdAsync(documentId, cancellationToken);
        if (document == null)
        {
            throw new InvalidOperationException($"Document {documentId} not found");
        }

        var docFields = document.ExtractedFields.Select(f => new ExtractedFieldDto
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

        var docSummary = new ExtractionSummaryDto
        {
            TotalFieldsExtracted = docFields.Count,
            HighConfidenceFields = docFields.Count(f => f.ConfidenceScore >= 0.7),
            LowConfidenceFields = docFields.Count(f => f.ConfidenceScore < 0.7),
            AverageConfidenceScore = docFields.Count > 0 ? docFields.Average(f => f.ConfidenceScore) : 0,
            ItemsRequiringReview = reviewItems.Count(r => r.Status == "Pending")
        };

        return new ExtractionResultDto
        {
            DocumentId = document.Id,
            DocumentFileName = document.FileName,
            ExtractedAt = DateTime.UtcNow,
            Fields = docFields,
            ReviewQueueItems = reviewItems,
            Summary = docSummary
        };
    }
}
