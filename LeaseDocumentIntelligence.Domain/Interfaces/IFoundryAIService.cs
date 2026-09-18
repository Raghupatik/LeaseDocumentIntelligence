namespace LeaseDocumentIntelligence.Domain.Interfaces;

using LeaseDocumentIntelligence.Domain.Models;

public interface IFoundryAIService
{
    Task<List<ExtractedField>> ExtractFieldsAsync(
        string documentText,
        Dictionary<string, string> fieldDefinitions,
        CancellationToken cancellationToken = default);

    Task<(ExtractedField Field, double Confidence)> ValidateExtractionAsync(
        string fieldName,
        string extractedValue,
        string documentText,
        CancellationToken cancellationToken = default);
}
