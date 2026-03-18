namespace Monica.Authority.Authorization.Annotations;

/// <remarks>
/// 配置权限Bit定义
/// </remarks>
[AttributeUsage(AttributeTargets.Field)]
public abstract class PermissionBitBaseAttribute(string permissionName, string? parentKey = null) : Attribute, IPermissionBitData
{
    /// <summary>
    /// 权限显示名
    /// </summary>
    public string PermissionName { get; set; } = permissionName;
    /// <summary>
    /// 权限描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 父权限Key，使用nameof(枚举)
    /// </summary>
    public string? ParentKey { get; set; } = parentKey;
}