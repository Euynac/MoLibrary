using Microsoft.AspNetCore.Authorization;
using Monica.Authority.Authorization.Abstractions;
using Monica.Authority.Localization;

namespace Monica.Authority.Authorization.Services.Support;

public class PolicyEnumPermissionRequirementHandler(
    IPermissionChecker permissionChecker,
    AuthorityMessageLocalizer authorityLocalizer)
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
            context.Fail(new AuthorizationFailureReason(
                this,
                authorityLocalizer.GetMissingPermissionMessage(requirement.PermissionName)));
        }
    }
}
