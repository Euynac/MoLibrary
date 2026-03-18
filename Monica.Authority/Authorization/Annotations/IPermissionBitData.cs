namespace Monica.Authority.Authorization.Annotations;

/// <summary>
/// 权限Bit信息
/// </summary>
public interface IPermissionBitData
{
    /// <summary>
    /// 权限显示名
    /// </summary>
    public string PermissionName { get; }
    /// <summary>
    /// 权限描述
    /// </summary>
    public string? Description { get;  }
    /// <summary>
    /// 父权限Key
    /// </summary>
    public string? ParentKey { get; }
}