using Microsoft.AspNetCore.Authorization;

namespace SmartEGov.Infrastructure.Authorization;

public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string OfficerOnly = "OfficerOnly";
    public const string CitizenOnly = "CitizenOnly";
    public const string AdminOrOfficer = "AdminOrOfficer";
    public const string AllRoles = "AllRoles";

    public static void AddAuthorizationPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(AdminOnly, policy =>
            policy.RequireRole("Admin"));

        options.AddPolicy(OfficerOnly, policy =>
            policy.RequireRole("Officer"));

        options.AddPolicy(CitizenOnly, policy =>
            policy.RequireRole("Citizen"));

        options.AddPolicy(AdminOrOfficer, policy =>
            policy.RequireRole("Admin", "Officer"));

        options.AddPolicy(AllRoles, policy =>
            policy.RequireRole("Admin", "Officer", "Citizen"));
    }
}
