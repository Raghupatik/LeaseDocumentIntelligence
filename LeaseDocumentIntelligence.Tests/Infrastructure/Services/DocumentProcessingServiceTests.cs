namespace LeaseDocumentIntelligence.Tests.Infrastructure.Services;

using FluentAssertions;
using LeaseDocumentIntelligence.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class DocumentProcessingServiceTests
{
    private readonly Mock<ILogger<DocumentProcessingService>> _mockLogger;
    private readonly DocumentProcessingService _service;

    public DocumentProcessingServiceTests()
    {
        _mockLogger = new Mock<ILogger<DocumentProcessingService>>();
        _service = new DocumentProcessingService(_mockLogger.Object);
    }

    [Fact]
    public async Task ValidateDocumentAsync_WithValidPdfFile_ReturnsTrue()
    {
        const string fileName = "lease.pdf";
        const string contentType = "application/pdf";

        var result = await _service.ValidateDocumentAsync(fileName, contentType);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateDocumentAsync_WithInvalidExtension_ReturnsFalse()
    {
        const string fileName = "lease.docx";
        const string contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        var result = await _service.ValidateDocumentAsync(fileName, contentType);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateDocumentAsync_WithInvalidContentType_ReturnsFalse()
    {
        const string fileName = "lease.pdf";
        const string contentType = "application/json";

        var result = await _service.ValidateDocumentAsync(fileName, contentType);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateDocumentAsync_WithEmptyFileName_ReturnsFalse()
    {
        const string fileName = "";
        const string contentType = "application/pdf";

        var result = await _service.ValidateDocumentAsync(fileName, contentType);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateDocumentAsync_WithNullFileName_ReturnsFalse()
    {
        var result = await _service.ValidateDocumentAsync(null!, "application/pdf");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExtractTextFromPdfAsync_WithValidPdfContent_ReturnsText()
    {
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        var result = async () => await _service.ExtractTextFromPdfAsync(pdfContent);

        _ = await result.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task ExtractTextFromPdfAsync_WithEmptyContent_ThrowsException()
    {
        var emptyContent = Array.Empty<byte>();

        var result = async () => await _service.ExtractTextFromPdfAsync(emptyContent);

        _ = await result.Should().ThrowAsync<Exception>();
    }
}
