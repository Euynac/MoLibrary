using Microsoft.AspNetCore.Authorization;
using Monica.Authority.Authorization.Abstractions;

namespace Monica.Authority.Authorization.Services.Support;

public class PolicyEnumPermissionRequirementHandler(IMoPermissionChecker permissionChecker)
    : AuthorizationHandler<PolicyEnumPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PolicyEnumPermissionRequirement requirement)
    {
        if (await permissionChecker.IsGrantedAsync(context.User, requirement.PermissionName))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(this, $"无{requirement.PermissionName}权限"));
        }
    }
}
