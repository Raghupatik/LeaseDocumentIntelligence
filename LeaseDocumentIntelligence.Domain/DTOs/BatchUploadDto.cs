namespace LeaseDocumentIntelligence.Domain.DTOs;

/// <summary>
/// Result of a batch upload operation
/// </summary>
public record BatchUploadResultDto(
    int TotalFiles,
    int SuccessCount,
    IReadOnlyList<BatchUploadItemResultDto> Results);

/// <summary>
/// Result of a single file in batch upload
/// </summary>
public record BatchUploadItemResultDto(
    Guid DocumentId,
    string FileName,
    bool IsSuccess,
    string? ErrorMessage);
