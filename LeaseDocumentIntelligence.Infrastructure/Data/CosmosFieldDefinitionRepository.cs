namespace LeaseDocumentIntelligence.Infrastructure.Data;

using LeaseDocumentIntelligence.Domain.Interfaces;
using LeaseDocumentIntelligence.Domain.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

/// <summary>
/// Cosmos DB repository for field definitions.
/// </summary>
public class CosmosFieldDefinitionRepository : IFieldDefinitionRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosFieldDefinitionRepository> _logger;
    private const string PartitionKey = "FieldDefinition";

    public CosmosFieldDefinitionRepository(
        CosmosClient cosmosClient,
        ILogger<CosmosFieldDefinitionRepository> logger)
    {
        _container = cosmosClient.GetContainer("LeaseDocuments", "FieldDefinitions");
        _logger = logger;
    }

    public async Task<List<FieldDefinition>> GetActiveFieldsAsync(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.pk = @pk AND c.isActive = true ORDER BY c.displayOrder")
            .WithParameter("@pk", PartitionKey);

        return await ExecuteQueryAsync(query, cancellationToken);
    }

    public async Task<List<FieldDefinition>> GetAllFieldsAsync(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.pk = @pk ORDER BY c.displayOrder")
            .WithParameter("@pk", PartitionKey);

        return await ExecuteQueryAsync(query, cancellationToken);
    }

    public async Task<List<FieldDefinition>> GetFieldsByCategoryAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.pk = @pk AND c.category = @category AND c.isActive = true ORDER BY c.displayOrder")
            .WithParameter("@pk", PartitionKey)
            .WithParameter("@category", category);

        return await ExecuteQueryAsync(query, cancellationToken);
    }

    public async Task<FieldDefinition?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<FieldDefinition>(
                id,
                new PartitionKey(PartitionKey),
                cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Field definition {FieldId} not found", id);
            return null;
        }
    }

    public async Task<FieldDefinition> CreateAsync(
        FieldDefinition field,
        CancellationToken cancellationToken = default)
    {
        field.Id = string.IsNullOrEmpty(field.Id) ? Guid.NewGuid().ToString() : field.Id;
        field.Pk = PartitionKey;
        field.CreatedAt = DateTime.UtcNow;
        field.UpdatedAt = DateTime.UtcNow;

        var response = await _container.CreateItemAsync(
            field,
            new PartitionKey(PartitionKey),
            cancellationToken: cancellationToken);

        _logger.LogInformation("Created field definition {FieldName} with ID {FieldId}", field.FieldName, field.Id);
        return response.Resource;
    }

    public async Task<FieldDefinition> UpdateAsync(
        FieldDefinition field,
        CancellationToken cancellationToken = default)
    {
        field.Pk = PartitionKey;
        field.UpdatedAt = DateTime.UtcNow;

        var response = await _container.UpsertItemAsync(
            field,
            new PartitionKey(PartitionKey),
            cancellationToken: cancellationToken);

        _logger.LogInformation("Updated field definition {FieldName}", field.FieldName);
        return response.Resource;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var field = await GetByIdAsync(id, cancellationToken);
        if (field != null)
        {
            field.IsActive = false;
            field.UpdatedAt = DateTime.UtcNow;
            await _container.UpsertItemAsync(
                field,
                new PartitionKey(PartitionKey),
                cancellationToken: cancellationToken);

            _logger.LogInformation("Soft deleted field definition {FieldId}", id);
        }
    }

    public async Task SeedDefaultFieldsAsync(CancellationToken cancellationToken = default)
    {
        var existingFields = await GetAllFieldsAsync(cancellationToken);
        if (existingFields.Count > 0)
        {
            _logger.LogInformation("Field definitions already exist, skipping seed");
            return;
        }

        var defaultFields = GetDefaultFieldDefinitions();
        foreach (var field in defaultFields)
        {
            await CreateAsync(field, cancellationToken);
        }

        _logger.LogInformation("Seeded {Count} default field definitions", defaultFields.Count);
    }

    private async Task<List<FieldDefinition>> ExecuteQueryAsync(
        QueryDefinition query,
        CancellationToken cancellationToken)
    {
        var results = new List<FieldDefinition>();
        using var iterator = _container.GetItemQueryIterator<FieldDefinition>(query);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    private static List<FieldDefinition> GetDefaultFieldDefinitions() =>
    [
        new FieldDefinition
        {
            Id = "base-rent-year1",
            FieldName = "BaseRentYear1",
            DisplayName = "Base Rent (Year 1)",
            Description = "Initial annual base rent amount",
            FieldType = FieldDataType.Currency,
            Category = "Rent",
            IsRequired = true,
            ConfidenceThreshold = 0.7,
            ExtractionHint = "Look for 'Base Rent', 'Annual Rent', or 'Minimum Rent' in Section 3 or 4",
            DisplayOrder = 1
        },
        new FieldDefinition
        {
            Id = "base-rent-schedule",
            FieldName = "BaseRentSchedule",
            DisplayName = "Base Rent Schedule",
            Description = "Complete rent schedule including escalations by year",
            FieldType = FieldDataType.Complex,
            Category = "Rent",
            IsRequired = true,
            ConfidenceThreshold = 0.6,
            ExtractionHint = "Look for rent tables, Exhibit A, or 'Rent Schedule'",
            DisplayOrder = 2
        },
        new FieldDefinition
        {
            Id = "rent-escalation",
            FieldName = "RentEscalation",
            DisplayName = "Rent Escalation",
            Description = "Annual rent increase terms (fixed %, CPI, etc.)",
            FieldType = FieldDataType.Text,
            Category = "Rent",
            IsRequired = true,
            ConfidenceThreshold = 0.7,
            ExtractionHint = "Look for 'annual increase', 'escalation', 'CPI adjustment'",
            DisplayOrder = 3
        },
        new FieldDefinition
        {
            Id = "lease-commencement-date",
            FieldName = "LeaseCommencementDate",
            DisplayName = "Lease Commencement Date",
            Description = "Date when the lease term begins",
            FieldType = FieldDataType.Date,
            Category = "Dates",
            IsRequired = true,
            ConfidenceThreshold = 0.8,
            ExtractionHint = "Look for 'Commencement Date', 'Lease Start', 'Term begins'",
            DisplayOrder = 10
        },
        new FieldDefinition
        {
            Id = "lease-expiration-date",
            FieldName = "LeaseExpirationDate",
            DisplayName = "Lease Expiration Date",
            Description = "Date when the lease term ends",
            FieldType = FieldDataType.Date,
            Category = "Dates",
            IsRequired = true,
            ConfidenceThreshold = 0.8,
            ExtractionHint = "Look for 'Expiration Date', 'Lease End', 'Term expires'",
            DisplayOrder = 11
        },
        new FieldDefinition
        {
            Id = "renewal-option",
            FieldName = "RenewalOption",
            DisplayName = "Renewal Option",
            Description = "Tenant's right to extend the lease term",
            FieldType = FieldDataType.Option,
            Category = "Options",
            IsRequired = false,
            ConfidenceThreshold = 0.65,
            ExtractionHint = "Look for 'Renewal Option', 'Extension Option', 'Option to Extend'",
            DisplayOrder = 20
        },
        new FieldDefinition
        {
            Id = "termination-option",
            FieldName = "TerminationOption",
            DisplayName = "Termination Option",
            Description = "Early termination rights and conditions",
            FieldType = FieldDataType.Option,
            Category = "Options",
            IsRequired = false,
            ConfidenceThreshold = 0.65,
            ExtractionHint = "Look for 'Termination Option', 'Early Termination', 'Break Clause'",
            DisplayOrder = 21
        },
        new FieldDefinition
        {
            Id = "cam-expense",
            FieldName = "CAMExpense",
            DisplayName = "CAM Expense",
            Description = "Common Area Maintenance charges",
            FieldType = FieldDataType.Currency,
            Category = "CAM",
            IsRequired = true,
            ConfidenceThreshold = 0.7,
            ExtractionHint = "Look for 'CAM', 'Common Area Maintenance', 'Operating Expenses'",
            DisplayOrder = 30
        },
        new FieldDefinition
        {
            Id = "cam-cap",
            FieldName = "CAMCap",
            DisplayName = "CAM Cap",
            Description = "Maximum annual increase in CAM charges",
            FieldType = FieldDataType.Percentage,
            Category = "CAM",
            IsRequired = false,
            ConfidenceThreshold = 0.7,
            ExtractionHint = "Look for 'CAM Cap', 'expense cap', 'controllable expense limit'",
            DisplayOrder = 31
        },
        new FieldDefinition
        {
            Id = "cam-exclusions",
            FieldName = "CAMExclusions",
            DisplayName = "CAM Exclusions",
            Description = "Items excluded from CAM charges",
            FieldType = FieldDataType.Text,
            Category = "CAM",
            IsRequired = false,
            ConfidenceThreshold = 0.6,
            ExtractionHint = "Look for 'excluded from Operating Expenses', 'CAM exclusions'",
            DisplayOrder = 32
        },
        new FieldDefinition
        {
            Id = "security-deposit",
            FieldName = "SecurityDeposit",
            DisplayName = "Security Deposit",
            Description = "Amount held as security for lease obligations",
            FieldType = FieldDataType.Currency,
            Category = "Deposits",
            IsRequired = true,
            ConfidenceThreshold = 0.8,
            ExtractionHint = "Look for 'Security Deposit', 'Deposit Amount'",
            DisplayOrder = 40
        },
        new FieldDefinition
        {
            Id = "notice-requirement-days",
            FieldName = "NoticeRequirementDays",
            DisplayName = "Notice Requirement (Days)",
            Description = "Required notice period for various lease actions",
            FieldType = FieldDataType.Duration,
            Category = "Notices",
            IsRequired = true,
            ConfidenceThreshold = 0.7,
            ExtractionHint = "Look for 'notice period', 'days notice', 'prior written notice'",
            DisplayOrder = 50
        },
        new FieldDefinition
        {
            Id = "co-tenancy-clause",
            FieldName = "CoTenancyClause",
            DisplayName = "Co-Tenancy Clause",
            Description = "Conditions tied to other tenant occupancy",
            FieldType = FieldDataType.Text,
            Category = "Conditions",
            IsRequired = false,
            ConfidenceThreshold = 0.6,
            ExtractionHint = "Look for 'Co-Tenancy', 'anchor tenant', 'occupancy requirement'",
            DisplayOrder = 60
        },
        new FieldDefinition
        {
            Id = "percentage-rent",
            FieldName = "PercentageRent",
            DisplayName = "Percentage Rent",
            Description = "Additional rent based on tenant sales",
            FieldType = FieldDataType.Percentage,
            Category = "Rent",
            IsRequired = false,
            ConfidenceThreshold = 0.7,
            ExtractionHint = "Look for 'Percentage Rent', 'overage rent', 'breakpoint'",
            DisplayOrder = 4
        },
        new FieldDefinition
        {
            Id = "parent-guarantee",
            FieldName = "ParentGuarantee",
            DisplayName = "Parent Guarantee",
            Description = "Guaranty from parent company or individual",
            FieldType = FieldDataType.Option,
            Category = "Guarantees",
            IsRequired = false,
            ConfidenceThreshold = 0.7,
            ExtractionHint = "Look for 'Guaranty', 'Guarantee', 'Guarantor'",
            DisplayOrder = 45
        }
    ];
}
