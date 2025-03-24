using Microsoft.AspNetCore.Authorization;
using Check = MoLibrary.Tool.Utils.Check;

namespace MoLibrary.Authority.Implements.Authorization;

public class EnumPermissionRequirement : IAuthorizationRequirement
{
    public string PermissionName { get; }

    public EnumPermissionRequirement(string permissionName)
    {
        Check.NotNull(permissionName, nameof(permissionName));

        PermissionName = permissionName;
    }

    public override string ToString()
    {
        return $"PermissionRequirement: {PermissionName}";
    }
}
