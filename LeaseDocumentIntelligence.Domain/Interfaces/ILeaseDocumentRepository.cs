namespace LeaseDocumentIntelligence.Domain.Interfaces;

using LeaseDocumentIntelligence.Domain.Models;

public interface ILeaseDocumentRepository
{
    Task<LeaseDocument> CreateAsync(
        LeaseDocument document,
        CancellationToken cancellationToken = default);

    Task<LeaseDocument?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<List<LeaseDocument>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        LeaseDocument document,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
