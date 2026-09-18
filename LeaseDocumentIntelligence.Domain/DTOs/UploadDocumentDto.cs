namespace LeaseDocumentIntelligence.Domain.DTOs;

public class UploadDocumentDto
{
    public required string FileName { get; set; }
    public required byte[] FileContent { get; set; }
    public required string ContentType { get; set; }
}
