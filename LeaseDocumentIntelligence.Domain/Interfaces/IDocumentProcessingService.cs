namespace LeaseDocumentIntelligence.Domain.Interfaces;

public interface IDocumentProcessingService
{
    Task<string> ExtractTextFromPdfAsync(byte[] fileContent, CancellationToken cancellationToken = default);
    Task<bool> ValidateDocumentAsync(string fileName, string contentType, CancellationToken cancellationToken = default);
}
