using Microsoft.AspNetCore.Authorization;
using Monica.Tool.Validation;

namespace Monica.Authority.Authorization.Services.Support;

public class PolicyEnumPermissionRequirement : IAuthorizationRequirement
{
    public string PermissionName { get; }

    public PolicyEnumPermissionRequirement(string permissionName)
    {
        Check.NotNull(permissionName, nameof(permissionName));

        PermissionName = permissionName;
    }

    public override string ToString()
    {
        return $"PermissionRequirement: {PermissionName}";
    }
}
