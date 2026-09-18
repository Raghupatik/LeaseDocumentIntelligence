namespace LeaseDocumentIntelligence.Domain.Constants;

public static class LeaseFieldDefinitions
{
    public static readonly Dictionary<string, LeaseFieldDefinition> Fields = new()
    {
        { "BaseRentYear1", new LeaseFieldDefinition { Name = "BaseRentYear1", Type = "Currency", Category = "Rent", IsRequired = true } },
        { "BaseRentSchedule", new LeaseFieldDefinition { Name = "BaseRentSchedule", Type = "Complex", Category = "Rent", IsRequired = true } },
        { "RentEscalation", new LeaseFieldDefinition { Name = "RentEscalation", Type = "Text", Category = "Rent", IsRequired = true } },
        { "LeaseCommencementDate", new LeaseFieldDefinition { Name = "LeaseCommencementDate", Type = "Date", Category = "Dates", IsRequired = true } },
        { "LeaseExpirationDate", new LeaseFieldDefinition { Name = "LeaseExpirationDate", Type = "Date", Category = "Dates", IsRequired = true } },
        { "RenewalOption", new LeaseFieldDefinition { Name = "RenewalOption", Type = "Option", Category = "Options", IsRequired = false } },
        { "TerminationOption", new LeaseFieldDefinition { Name = "TerminationOption", Type = "Option", Category = "Options", IsRequired = false } },
        { "CAMExpense", new LeaseFieldDefinition { Name = "CAMExpense", Type = "Currency", Category = "CAM", IsRequired = true } },
        { "CAMCap", new LeaseFieldDefinition { Name = "CAMCap", Type = "Percentage", Category = "CAM", IsRequired = false } },
        { "CAMExclusions", new LeaseFieldDefinition { Name = "CAMExclusions", Type = "Text", Category = "CAM", IsRequired = false } },
        { "SecurityDeposit", new LeaseFieldDefinition { Name = "SecurityDeposit", Type = "Currency", Category = "Deposits", IsRequired = true } },
        { "ParentGuarantee", new LeaseFieldDefinition { Name = "ParentGuarantee", Type = "Option", Category = "Guarantees", IsRequired = false } },
        { "NoticeRequirementDays", new LeaseFieldDefinition { Name = "NoticeRequirementDays", Type = "Duration", Category = "Notices", IsRequired = true } },
        { "CoTenancyClause", new LeaseFieldDefinition { Name = "CoTenancyClause", Type = "Text", Category = "Conditions", IsRequired = false } },
        { "PercentageRent", new LeaseFieldDefinition { Name = "PercentageRent", Type = "Percentage", Category = "Rent", IsRequired = false } },
    };
}

public class LeaseFieldDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
}
