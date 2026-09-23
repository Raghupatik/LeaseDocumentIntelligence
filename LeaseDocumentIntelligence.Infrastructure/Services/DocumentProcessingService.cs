using LeaseDocumentIntelligence.Domain.Interfaces;

namespace LeaseDocumentIntelligence.Infrastructure.Services;

using System.IO;
using Microsoft.Extensions.Logging;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

public class DocumentProcessingService : IDocumentProcessingService
{
    private readonly ILogger<DocumentProcessingService> _logger;
    private static readonly string[] AllowedContentTypes = new[] { "application/pdf" };
    private const long MaxFileSizeBytes = 50 * 1024 * 1024;

    public DocumentProcessingService(ILogger<DocumentProcessingService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExtractTextFromPdfAsync(byte[] fileContent, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var text = new StringBuilder();
                using var ms = new MemoryStream(fileContent);
                using var document = PdfDocument.Open(ms);

                int pageIndex = 1;
                foreach (var page in document.GetPages())
                {
                    text.AppendLine($"--- Page {pageIndex} ---");
                    // PdfPig exposes page text via the Text property
                    text.AppendLine(page.Text ?? string.Empty);
                    pageIndex++;
                }                return text.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from PDF");
                throw;
            }
        }, cancellationToken);
    }

    public Task<bool> ValidateDocumentAsync(string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            _logger.LogWarning("Validation failed: Empty filename");
            return Task.FromResult(false);
        }

        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Validation failed: Invalid file extension for {FileName}", fileName);
            return Task.FromResult(false);
        }

        // Accept if content type is PDF or empty (Blazor InputFile may not set it)
        if (!string.IsNullOrEmpty(contentType) && !AllowedContentTypes.Contains(contentType))
        {
            _logger.LogWarning("Validation failed: Invalid content type {ContentType}", contentType);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }
}
