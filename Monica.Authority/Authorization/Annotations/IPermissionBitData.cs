namespace Monica.Authority.Authorization.Annotations;

/// <summary>
/// Permission bit metadata
/// </summary>
public interface IPermissionBitData
{
    /// <summary>
    /// Permission display name
    /// </summary>
    public string PermissionName { get; }
    /// <summary>
    /// Permission description
    /// </summary>
    public string? Description { get;  }
    /// <summary>
    /// Parent permission key
    /// </summary>
    public string? ParentKey { get; }
}
