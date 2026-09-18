namespace LeaseDocumentIntelligence.Domain.Constants;

/// <summary>
/// Authorization roles for Lease Document Intelligence
/// </summary>
public static class AuthorizationRoles
{
    /// <summary>
    /// Administrator role - full system access
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// Lease Analyst role - can upload and extract lease documents
    /// </summary>
    public const string LeaseAnalyst = "LeaseAnalyst";

    /// <summary>
    /// Reviewer role - can review and approve extracted fields
    /// </summary>
    public const string Reviewer = "Reviewer";

    /// <summary>
    /// Viewer role - read-only access to results
    /// </summary>
    public const string Viewer = "Viewer";

    /// <summary>
    /// All valid roles
    /// </summary>
    public static readonly string[] AllRoles = { Admin, LeaseAnalyst, Reviewer, Viewer };
}

/// <summary>
/// Authorization policies for Lease Document Intelligence
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Policy: Only administrators can access
    /// </summary>
    public const string AdminOnly = "AdminOnly";

    /// <summary>
    /// Policy: Lease analysts or administrators
    /// </summary>
    public const string LeaseAnalystOrAdmin = "LeaseAnalystOrAdmin";

    /// <summary>
    /// Policy: Reviewers or administrators
    /// </summary>
    public const string ReviewerOrAdmin = "ReviewerOrAdmin";

    /// <summary>
    /// Policy: Any authenticated user
    /// </summary>
    public const string AllAuthenticatedUsers = "AllAuthenticatedUsers";
}
