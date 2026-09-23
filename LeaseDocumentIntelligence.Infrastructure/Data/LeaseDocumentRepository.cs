namespace LeaseDocumentIntelligence.Infrastructure.Data;

using LeaseDocumentIntelligence.Domain.Interfaces;
using LeaseDocumentIntelligence.Domain.Models;
using Microsoft.Extensions.Logging;

public class LeaseDocumentRepository : ILeaseDocumentRepository
{
    private readonly ILogger<LeaseDocumentRepository> _logger;
    private static readonly Dictionary<Guid, LeaseDocument> _documents = new();

    public LeaseDocumentRepository(ILogger<LeaseDocumentRepository> logger)
    {
        _logger = logger;
    }

    public Task<LeaseDocument> CreateAsync(
        LeaseDocument document,
        CancellationToken cancellationToken = default)
    {
        document.Id = Guid.NewGuid();
        document.UploadedAt = DateTime.UtcNow;
        _documents[document.Id] = document;
        return Task.FromResult(document);
    }

    public Task<LeaseDocument?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _documents.TryGetValue(id, out var document);
        if (document == null)
        {
            _logger.LogWarning("Document {DocumentId} not found", id);
        }
        return Task.FromResult(document);
    }

    public Task<List<LeaseDocument>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var documents = _documents.Values.ToList();
        return Task.FromResult(documents);
    }

    public Task UpdateAsync(
        LeaseDocument document,
        CancellationToken cancellationToken = default)
    {
        if (_documents.ContainsKey(document.Id))
        {
            _documents[document.Id] = document;
        }
        else
        {
            _logger.LogWarning("Document {DocumentId} not found for update", document.Id);
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!_documents.Remove(id))
        {
            _logger.LogWarning("Document {DocumentId} not found for deletion", id);
        }
        return Task.CompletedTask;
    }
}
