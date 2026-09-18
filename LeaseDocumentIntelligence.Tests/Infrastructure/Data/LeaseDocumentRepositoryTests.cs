namespace LeaseDocumentIntelligence.Tests.Infrastructure.Data;

using FluentAssertions;
using LeaseDocumentIntelligence.Domain.Models;
using LeaseDocumentIntelligence.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class LeaseDocumentRepositoryTests
{
    private readonly Mock<ILogger<LeaseDocumentRepository>> _mockLogger;
    private LeaseDocumentRepository _repository;

    public LeaseDocumentRepositoryTests()
    {
        _mockLogger = new Mock<ILogger<LeaseDocumentRepository>>();
        ResetRepository();
    }

    private void ResetRepository()
    {
        _repository = new LeaseDocumentRepository(_mockLogger.Object);
    }

    [Fact]
    public async Task CreateAsync_WithValidDocument_CreatesAndReturnsDocument()
    {
        var document = new LeaseDocument
        {
            FileName = "lease.pdf",
            FileContent = new byte[] { 0x25, 0x50, 0x44, 0x46 },
            ContentType = "application/pdf",
            FileSize = 1024,
            UploadedBy = "testuser"
        };

        var created = await _repository.CreateAsync(document);

        created.Id.Should().NotBeEmpty();
        created.FileName.Should().Be("lease.pdf");
        created.UploadedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsDocument()
    {
        var document = new LeaseDocument
        {
            FileName = "lease.pdf",
            FileContent = new byte[] { 0x25, 0x50, 0x44, 0x46 },
            ContentType = "application/pdf",
            FileSize = 1024
        };

        var created = await _repository.CreateAsync(document);
        var retrieved = await _repository.GetByIdAsync(created.Id);

        retrieved.Should().NotBeNull();
        retrieved!.FileName.Should().Be("lease.pdf");
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        var retrieved = await _repository.GetByIdAsync(Guid.NewGuid());

        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllDocuments()
    {
        ResetRepository();
        var doc1 = new LeaseDocument { FileName = "lease1.pdf", FileContent = [], ContentType = "application/pdf" };
        var doc2 = new LeaseDocument { FileName = "lease2.pdf", FileContent = [], ContentType = "application/pdf" };

        await _repository.CreateAsync(doc1);
        await _repository.CreateAsync(doc2);

        var documents = await _repository.GetAllAsync();

        documents.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task UpdateAsync_WithValidDocument_UpdatesSuccessfully()
    {
        var document = new LeaseDocument
        {
            FileName = "lease.pdf",
            FileContent = new byte[] { 0x25, 0x50, 0x44, 0x46 },
            ContentType = "application/pdf",
            FileSize = 1024
        };

        var created = await _repository.CreateAsync(document);
        created.Status = DocumentStatus.Completed;

        await _repository.UpdateAsync(created);

        var retrieved = await _repository.GetByIdAsync(created.Id);
        retrieved!.Status.Should().Be(DocumentStatus.Completed);
    }

    [Fact]
    public async Task DeleteAsync_WithValidId_DeletesDocument()
    {
        var document = new LeaseDocument
        {
            FileName = "lease.pdf",
            FileContent = new byte[] { 0x25, 0x50, 0x44, 0x46 },
            ContentType = "application/pdf",
            FileSize = 1024
        };

        var created = await _repository.CreateAsync(document);
        await _repository.DeleteAsync(created.Id);

        var retrieved = await _repository.GetByIdAsync(created.Id);
        retrieved.Should().BeNull();
    }
}
