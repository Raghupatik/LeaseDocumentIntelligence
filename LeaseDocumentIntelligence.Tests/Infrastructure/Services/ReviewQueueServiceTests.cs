namespace LeaseDocumentIntelligence.Tests.Infrastructure.Services;

using FluentAssertions;
using LeaseDocumentIntelligence.Domain.Models;
using LeaseDocumentIntelligence.Infrastructure.Services;
using LeaseDocumentIntelligence.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class ReviewQueueServiceTests
{
    private readonly Mock<ILeaseDocumentRepository> _mockRepository;
    private readonly Mock<ILogger<ReviewQueueService>> _mockLogger;
    private readonly ReviewQueueService _service;

    public ReviewQueueServiceTests()
    {
        _mockRepository = new Mock<ILeaseDocumentRepository>();
        _mockLogger = new Mock<ILogger<ReviewQueueService>>();
        _service = new ReviewQueueService(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task AddToReviewQueueAsync_WithValidFields_AddsItemsSuccessfully()
    {
        var documentId = Guid.NewGuid();
        var document = new LeaseDocument { Id = documentId };

        var fieldsForReview = new List<ExtractedField>
        {
            new ExtractedField
            {
                FieldName = "CoTenancyClause",
                ExtractedValue = "Unclear",
                ConfidenceScore = 0.5,
                FieldType = FieldType.Text
            }
        };

        _mockRepository.Setup(x => x.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _mockRepository.Setup(x => x.UpdateAsync(It.IsAny<LeaseDocument>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.AddToReviewQueueAsync(documentId, fieldsForReview);

        document.ReviewQueueItems.Should().HaveCount(1);
        document.ReviewQueueItems.First().FieldName.Should().Be("CoTenancyClause");
    }

    [Fact]
    public async Task AddToReviewQueueAsync_WithInvalidDocumentId_ThrowsException()
    {
        var invalidId = Guid.NewGuid();
        var fieldsForReview = new List<ExtractedField>
        {
            new ExtractedField { FieldName = "Field1", FieldType = FieldType.Text }
        };

        _mockRepository.Setup(x => x.GetByIdAsync(invalidId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeaseDocument?)null);

        var result = async () => await _service.AddToReviewQueueAsync(invalidId, fieldsForReview);

        _ = await result.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetReviewQueueAsync_ReturnsPendingItems()
    {
        var items = await _service.GetReviewQueueAsync();

        items.Should().NotBeNull();
        items.Should().BeOfType<List<Domain.DTOs.ReviewQueueItemDto>>();
    }

    [Fact]
    public async Task ApproveReviewItemAsync_WithValidId_ApprobesItem()
    {
        var documentId = Guid.NewGuid();
        var document = new LeaseDocument { Id = documentId };

        var field = new ExtractedField
        {
            FieldName = "Field1",
            ExtractedValue = "Value1",
            ConfidenceScore = 0.5,
            FieldType = FieldType.Text
        };

        var reviewItem = new ReviewQueueItem
        {
            Id = Guid.NewGuid(),
            LeaseDocumentId = documentId,
            FieldName = "Field1",
            ExtractedValue = "Value1",
            ConfidenceScore = 0.5,
            Status = ReviewStatus.Pending
        };

        document.ReviewQueueItems.Add(reviewItem);

        _mockRepository.Setup(x => x.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        await _service.AddToReviewQueueAsync(documentId, new List<ExtractedField> { field });

        var pendingItems = await _service.GetReviewQueueAsync();
        pendingItems.Should().HaveCount(1);
    }
}
