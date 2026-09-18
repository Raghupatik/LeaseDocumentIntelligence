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
        return Task.Run(() =>
        {
            document.Id = Guid.NewGuid();
            document.UploadedAt = DateTime.UtcNow;
            _documents[document.Id] = document;            return document;
        }, cancellationToken);
    }

    public Task<LeaseDocument?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            _documents.TryGetValue(id, out var document);
            if (document != null)
            {            }
            else
            {
                _logger.LogWarning("Document {DocumentId} not found", id);
            }
            return document;
        }, cancellationToken);
    }

    public Task<List<LeaseDocument>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var documents = _documents.Values.ToList();            return documents;
        }, cancellationToken);
    }

    public Task UpdateAsync(
        LeaseDocument document,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (_documents.ContainsKey(document.Id))
            {
                _documents[document.Id] = document;            }
            else
            {
                _logger.LogWarning("Document {DocumentId} not found for update", document.Id);
            }
        }, cancellationToken);
    }

    public Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (_documents.Remove(id))
            {            }
            else
            {
                _logger.LogWarning("Document {DocumentId} not found for deletion", id);
            }
        }, cancellationToken);
    }
}
