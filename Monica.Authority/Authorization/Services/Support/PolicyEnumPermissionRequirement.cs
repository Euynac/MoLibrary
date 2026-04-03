using Microsoft.AspNetCore.Authorization;

namespace Monica.Authority.Authorization.Services.Support;

public class PolicyEnumPermissionRequirement : IAuthorizationRequirement
{
    public string PermissionName { get; }

    public PolicyEnumPermissionRequirement(string permissionName)
    {
        ArgumentNullException.ThrowIfNull(permissionName);

        PermissionName = permissionName;
    }

    public override string ToString()
    {
        return $"PermissionRequirement: {PermissionName}";
    }
}
