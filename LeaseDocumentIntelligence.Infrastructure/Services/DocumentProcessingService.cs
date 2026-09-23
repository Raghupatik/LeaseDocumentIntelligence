using LeaseDocumentIntelligence.Domain.Interfaces;

namespace LeaseDocumentIntelligence.Infrastructure.Services;

using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using UglyToad.PdfPig;

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

                // Extract regular page text
                int pageIndex = 1;
                foreach (var page in document.GetPages())
                {
                    text.AppendLine($"--- Page {pageIndex} ---");
                    text.AppendLine(page.Text ?? string.Empty);
                    pageIndex++;
                }

                // Extract AcroForm field values (fillable PDF forms)
                // This handles PDFs where values are in form fields, not the text layer
                if (document.TryGetForm(out var form) && form.Fields.Count > 0)
                {
                    text.AppendLine();
                    text.AppendLine("--- Form Field Values ---");
                    _logger.LogInformation("Found {Count} AcroForm fields in PDF", form.Fields.Count);

                    int fieldIndex = 0;
                    foreach (var field in form.Fields)
                    {
                        fieldIndex++;
                        // Most lease forms use text fields - extract those
                        if (field is UglyToad.PdfPig.AcroForms.Fields.AcroTextField textField)
                        {
                            // Information.PartialName contains the field name in PdfPig
                            var fieldName = textField.Information?.PartialName ?? $"Field_{fieldIndex}";
                            var fieldValue = textField.Value;

                            if (!string.IsNullOrWhiteSpace(fieldValue))
                            {
                                text.AppendLine($"{fieldName}: {fieldValue}");
                                _logger.LogDebug("Form field '{Name}' = '{Value}'", fieldName, fieldValue);
                            }
                        }
                        else if (field is UglyToad.PdfPig.AcroForms.Fields.AcroCheckboxField checkboxField)
                        {
                            var fieldName = checkboxField.Information?.PartialName ?? $"Checkbox_{fieldIndex}";
                            text.AppendLine($"{fieldName}: {(checkboxField.IsChecked ? "Yes" : "No")}");
                        }
                    }
                }

                return text.ToString();
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
