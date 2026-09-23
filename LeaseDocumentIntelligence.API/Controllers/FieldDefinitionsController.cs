namespace LeaseDocumentIntelligence.API.Controllers;

using LeaseDocumentIntelligence.Domain.DTOs;
using LeaseDocumentIntelligence.Domain.Interfaces;
using LeaseDocumentIntelligence.Domain.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Manages field definitions for lease extraction.
/// Admin-only endpoints for configuring which fields to extract.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")]
public class FieldDefinitionsController : ControllerBase
{
    private readonly IFieldDefinitionRepository _repository;
    private readonly ILogger<FieldDefinitionsController> _logger;

    public FieldDefinitionsController(
        IFieldDefinitionRepository repository,
        ILogger<FieldDefinitionsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all active field definitions.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<FieldDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FieldDefinitionDto>>> GetActiveFields(
        CancellationToken cancellationToken)
    {
        var fields = await _repository.GetActiveFieldsAsync(cancellationToken);
        return Ok(fields.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Gets all field definitions including inactive.
    /// </summary>
    [HttpGet("all")]
    [ProducesResponseType(typeof(List<FieldDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FieldDefinitionDto>>> GetAllFields(
        CancellationToken cancellationToken)
    {
        var fields = await _repository.GetAllFieldsAsync(cancellationToken);
        return Ok(fields.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Gets field definitions by category.
    /// </summary>
    [HttpGet("category/{category}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<FieldDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<FieldDefinitionDto>>> GetFieldsByCategory(
        string category,
        CancellationToken cancellationToken)
    {
        var fields = await _repository.GetFieldsByCategoryAsync(category, cancellationToken);
        return Ok(fields.Select(MapToDto).ToList());
    }

    /// <summary>
    /// Gets a specific field definition by ID.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(FieldDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FieldDefinitionDto>> GetField(
        string id,
        CancellationToken cancellationToken)
    {
        var field = await _repository.GetByIdAsync(id, cancellationToken);
        if (field == null)
        {
            return NotFound();
        }
        return Ok(MapToDto(field));
    }

    /// <summary>
    /// Creates a new field definition.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(FieldDefinitionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FieldDefinitionDto>> CreateField(
        CreateFieldDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.Identity?.Name ?? "system";

        var field = new FieldDefinition
        {
            FieldName = request.FieldName,
            DisplayName = request.DisplayName,
            Description = request.Description,
            FieldType = Enum.TryParse<FieldDataType>(request.FieldType, out var fieldType)
                ? fieldType
                : FieldDataType.Text,
            Category = request.Category,
            IsRequired = request.IsRequired,
            ConfidenceThreshold = request.ConfidenceThreshold,
            ExtractionHint = request.ExtractionHint,
            ValidationRegex = request.ValidationRegex,
            DisplayOrder = request.DisplayOrder,
            CreatedBy = userId,
            UpdatedBy = userId
        };

        var created = await _repository.CreateAsync(field, cancellationToken);
        _logger.LogInformation("Field definition {FieldName} created by {User}", field.FieldName, userId);

        return CreatedAtAction(
            nameof(GetField),
            new { id = created.Id },
            MapToDto(created));
    }

    /// <summary>
    /// Updates an existing field definition.
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(FieldDefinitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FieldDefinitionDto>> UpdateField(
        string id,
        UpdateFieldDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return NotFound();
        }

        var userId = User.Identity?.Name ?? "system";

        // Apply partial updates
        if (request.DisplayName != null) existing.DisplayName = request.DisplayName;
        if (request.Description != null) existing.Description = request.Description;
        if (request.FieldType != null && Enum.TryParse<FieldDataType>(request.FieldType, out var fieldType))
            existing.FieldType = fieldType;
        if (request.Category != null) existing.Category = request.Category;
        if (request.IsRequired.HasValue) existing.IsRequired = request.IsRequired.Value;
        if (request.IsActive.HasValue) existing.IsActive = request.IsActive.Value;
        if (request.ConfidenceThreshold.HasValue) existing.ConfidenceThreshold = request.ConfidenceThreshold.Value;
        if (request.ExtractionHint != null) existing.ExtractionHint = request.ExtractionHint;
        if (request.ValidationRegex != null) existing.ValidationRegex = request.ValidationRegex;
        if (request.DisplayOrder.HasValue) existing.DisplayOrder = request.DisplayOrder.Value;

        existing.UpdatedBy = userId;

        var updated = await _repository.UpdateAsync(existing, cancellationToken);
        _logger.LogInformation("Field definition {FieldId} updated by {User}", id, userId);

        return Ok(MapToDto(updated));
    }

    /// <summary>
    /// Deletes (deactivates) a field definition.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteField(
        string id,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return NotFound();
        }

        await _repository.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("Field definition {FieldId} deleted by {User}", id, User.Identity?.Name ?? "system");

        return NoContent();
    }

    /// <summary>
    /// Seeds default field definitions (for initial setup).
    /// </summary>
    [HttpPost("seed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SeedDefaults(CancellationToken cancellationToken)
    {
        await _repository.SeedDefaultFieldsAsync(cancellationToken);
        return Ok(new { message = "Default field definitions seeded" });
    }

    private static FieldDefinitionDto MapToDto(FieldDefinition field) => new()
    {
        Id = field.Id,
        FieldName = field.FieldName,
        DisplayName = field.DisplayName,
        Description = field.Description,
        FieldType = field.FieldType.ToString(),
        Category = field.Category,
        IsRequired = field.IsRequired,
        IsActive = field.IsActive,
        ConfidenceThreshold = field.ConfidenceThreshold,
        ExtractionHint = field.ExtractionHint,
        ValidationRegex = field.ValidationRegex,
        DisplayOrder = field.DisplayOrder
    };
}
