using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Kaleido.Http.FunctionalTests.Authorization;

/// <summary>
/// A host's own ASP.NET authorization handler: a <c>superuser</c> satisfies any role requirement.
/// Kaleido authorizes through ASP.NET (#574), so the handler admits the same callers on a typed
/// route, through generic execute and in discovery.
/// </summary>
internal sealed class SuperuserRoleHandler
    : AuthorizationHandler<RolesAuthorizationRequirement>
{
    public const string Role = "superuser";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RolesAuthorizationRequirement requirement)
    {
        if (context.User.IsInRole(Role))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
