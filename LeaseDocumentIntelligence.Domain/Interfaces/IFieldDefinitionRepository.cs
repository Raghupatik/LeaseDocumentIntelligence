namespace LeaseDocumentIntelligence.Domain.Interfaces;

using LeaseDocumentIntelligence.Domain.Models;

/// <summary>
/// Repository for managing field definitions in Cosmos DB.
/// </summary>
public interface IFieldDefinitionRepository
{
    /// <summary>
    /// Gets all active field definitions.
    /// </summary>
    Task<List<FieldDefinition>> GetActiveFieldsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all field definitions (including inactive).
    /// </summary>
    Task<List<FieldDefinition>> GetAllFieldsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets field definitions by category.
    /// </summary>
    Task<List<FieldDefinition>> GetFieldsByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a field definition by ID.
    /// </summary>
    Task<FieldDefinition?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new field definition.
    /// </summary>
    Task<FieldDefinition> CreateAsync(FieldDefinition field, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing field definition.
    /// </summary>
    Task<FieldDefinition> UpdateAsync(FieldDefinition field, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a field definition (soft delete - sets IsActive = false).
    /// </summary>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds default field definitions if none exist.
    /// </summary>
    Task SeedDefaultFieldsAsync(CancellationToken cancellationToken = default);
}
