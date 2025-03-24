using Microsoft.AspNetCore.Authorization;
using MoLibrary.Authority.Authorization;

namespace MoLibrary.Authority.Implements.Authorization;

public class EnumPermissionRequirementHandler(IMoPermissionChecker permissionChecker)
    : AuthorizationHandler<EnumPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EnumPermissionRequirement requirement)
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
