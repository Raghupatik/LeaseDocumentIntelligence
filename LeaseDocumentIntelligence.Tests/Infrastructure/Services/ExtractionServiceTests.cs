namespace LeaseDocumentIntelligence.Tests.Infrastructure.Services;

using FluentAssertions;
using LeaseDocumentIntelligence.Domain.Interfaces;
using LeaseDocumentIntelligence.Domain.Models;
using LeaseDocumentIntelligence.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class ExtractionServiceTests
{
    private readonly Mock<IDocumentProcessingService> _mockDocProcessing;
    private readonly Mock<IFoundryAIService> _mockFoundryAI;
    private readonly Mock<IReviewQueueService> _mockReviewQueue;
    private readonly Mock<ILeaseDocumentRepository> _mockRepository;
    private readonly Mock<IDocumentMetadataRepository> _mockMetadataRepository;
    private readonly Mock<IFieldDefinitionRepository> _mockFieldDefinitionRepo;
    private readonly Mock<IVectorSearchService> _mockSearchService;
    private readonly Mock<ILogger<ExtractionService>> _mockLogger;
    private readonly ExtractionService _service;

    public ExtractionServiceTests()
    {
        _mockDocProcessing = new Mock<IDocumentProcessingService>();
        _mockFoundryAI = new Mock<IFoundryAIService>();
        _mockReviewQueue = new Mock<IReviewQueueService>();
        _mockRepository = new Mock<ILeaseDocumentRepository>();
        _mockMetadataRepository = new Mock<IDocumentMetadataRepository>();
        _mockFieldDefinitionRepo = new Mock<IFieldDefinitionRepository>();
        _mockSearchService = new Mock<IVectorSearchService>();
        _mockLogger = new Mock<ILogger<ExtractionService>>();

        // Setup default to return empty field definitions (use hardcoded)
        _mockFieldDefinitionRepo.Setup(x => x.GetActiveFieldsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _service = new ExtractionService(
            _mockDocProcessing.Object,
            _mockFoundryAI.Object,
            _mockReviewQueue.Object,
            _mockRepository.Object,
            _mockMetadataRepository.Object,
            _mockFieldDefinitionRepo.Object,
            _mockSearchService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ExtractLeaseDataAsync_WithValidDocument_ReturnsExtractionResult()
    {
        var documentId = Guid.NewGuid();
        var document = new LeaseDocument
        {
            Id = documentId,
            FileName = "lease.pdf",
            FileContent = new byte[] { 0x25, 0x50, 0x44, 0x46 },
            ContentType = "application/pdf",
            FileSize = 1024,
            Status = DocumentStatus.Pending
        };

        var extractedFields = new List<ExtractedField>
        {
            new ExtractedField
            {
                Id = Guid.NewGuid(),
                LeaseDocumentId = documentId,
                FieldName = "BaseRentYear1",
                ExtractedValue = "$100,000",
                ConfidenceScore = 0.95,
                PageReference = "Page 1",
                ExtractedAt = DateTime.UtcNow,
                FieldType = FieldType.Currency,
                RequiresReview = false
            }
        };

        _mockDocProcessing.Setup(x => x.ExtractTextFromPdfAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Lease document text");

        _mockFoundryAI.Setup(x => x.ExtractFieldsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extractedFields);

        _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<LeaseDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockRepository.Setup(x => x.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var result = await _service.ExtractLeaseDataAsync(document);

        result.Should().NotBeNull();
        result.DocumentId.Should().Be(documentId);
        result.Fields.Should().HaveCount(1);
        result.Summary.TotalFieldsExtracted.Should().Be(1);
    }

    [Fact]
    public async Task ExtractLeaseDataAsync_WithLowConfidenceFields_AddsToReviewQueue()
    {
        var documentId = Guid.NewGuid();
        var document = new LeaseDocument
        {
            Id = documentId,
            FileName = "lease.pdf",
            FileContent = new byte[] { 0x25, 0x50, 0x44, 0x46 },
            ContentType = "application/pdf",
            FileSize = 1024,
            Status = DocumentStatus.Pending
        };

        var extractedFields = new List<ExtractedField>
        {
            new ExtractedField
            {
                Id = Guid.NewGuid(),
                LeaseDocumentId = documentId,
                FieldName = "CoTenancyClause",
                ExtractedValue = "Unclear clause",
                ConfidenceScore = 0.45,
                PageReference = "Page 15",
                ExtractedAt = DateTime.UtcNow,
                FieldType = FieldType.Text,
                RequiresReview = true
            }
        };

        _mockDocProcessing.Setup(x => x.ExtractTextFromPdfAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Lease document text");

        _mockFoundryAI.Setup(x => x.ExtractFieldsAsync(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extractedFields);

        _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<LeaseDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockRepository.Setup(x => x.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        await _service.ExtractLeaseDataAsync(document);

        _mockReviewQueue.Verify(x => x.AddToReviewQueueAsync(documentId, It.IsAny<List<ExtractedField>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetExtractionResultAsync_WithInvalidDocumentId_ThrowsException()
    {
        var invalidId = Guid.NewGuid();
        _mockRepository.Setup(x => x.GetByIdAsync(invalidId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaseDocument?)null);

        var result = async () => await _service.GetExtractionResultAsync(invalidId);

        _ = await result.Should().ThrowAsync<InvalidOperationException>();
    }
}
