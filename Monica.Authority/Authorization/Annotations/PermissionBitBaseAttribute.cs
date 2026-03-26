namespace Monica.Authority.Authorization.Annotations;

/// <remarks>
/// Configure permission bit metadata
/// </remarks>
[AttributeUsage(AttributeTargets.Field)]
public abstract class PermissionBitBaseAttribute(string permissionName, string? parentKey = null) : Attribute, IPermissionBitData
{
    /// <summary>
    /// Friendly name for the permission
    /// </summary>
    public string PermissionName { get; set; } = permissionName;
    /// <summary>
    /// Permission description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Parent permission key (use nameof(enum))
    /// </summary>
    public string? ParentKey { get; set; } = parentKey;
}
