namespace LeaseDocumentIntelligence.Domain.DTOs;

/// <summary>
/// DTO for field definition CRUD operations.
/// </summary>
public class FieldDefinitionDto
{
    public string Id { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FieldType { get; set; } = "Text";
    public string Category { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; } = true;
    public double ConfidenceThreshold { get; set; } = 0.7;
    public string ExtractionHint { get; set; } = string.Empty;
    public string? ValidationRegex { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Request DTO for creating a new field definition.
/// </summary>
public class CreateFieldDefinitionRequest
{
    public required string FieldName { get; set; }
    public required string DisplayName { get; set; }
    public string Description { get; set; } = string.Empty;
    public string FieldType { get; set; } = "Text";
    public required string Category { get; set; }
    public bool IsRequired { get; set; }
    public double ConfidenceThreshold { get; set; } = 0.7;
    public string ExtractionHint { get; set; } = string.Empty;
    public string? ValidationRegex { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Request DTO for updating a field definition.
/// </summary>
public class UpdateFieldDefinitionRequest
{
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? FieldType { get; set; }
    public string? Category { get; set; }
    public bool? IsRequired { get; set; }
    public bool? IsActive { get; set; }
    public double? ConfidenceThreshold { get; set; }
    public string? ExtractionHint { get; set; }
    public string? ValidationRegex { get; set; }
    public int? DisplayOrder { get; set; }
}
