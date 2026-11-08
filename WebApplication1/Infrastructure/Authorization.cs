using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Services;

namespace WebApplication1.Infrastructure;

/// <summary>
/// Protège une page par permission :  [HasPermission(Permissions.ProductsView)]
/// </summary>
public class HasPermissionAttribute(string permission) : AuthorizeAttribute(PermissionPolicyProvider.Prefix + permission);

public record PermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>Crée à la volée une policy "perm:xxx" pour chaque permission.</summary>
public class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public const string Prefix = "perm:";

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName[Prefix.Length..]))
                .Build();
        return await base.GetPolicyAsync(policyName);
    }
}

/// <summary>Vérifie en base (à chaque requête) que le rôle de l'employé possède la permission.</summary>
public class PermissionHandler(UserAccessService access) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (await access.CanAsync(requirement.Permission))
            context.Succeed(requirement);
    }
}
