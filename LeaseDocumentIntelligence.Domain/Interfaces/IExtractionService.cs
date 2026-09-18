namespace LeaseDocumentIntelligence.Domain.Interfaces;

using LeaseDocumentIntelligence.Domain.DTOs;
using LeaseDocumentIntelligence.Domain.Models;

public interface IExtractionService
{
    Task<ExtractionResultDto> ExtractLeaseDataAsync(
        LeaseDocument document,
        CancellationToken cancellationToken = default);

    Task<ExtractionResultDto> GetExtractionResultAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
}
