namespace MoLibrary.UI.Services;

/// <summary>
/// MoLibrary用户上下文服务 - 提供当前用户信息
/// </summary>
public class MoUserContextService
{
    /// <summary>
    /// 用户名称
    /// </summary>
    public string UserName => "Mo User";

    /// <summary>
    /// 用户角色
    /// </summary>
    public string UserRole => "Administrator";

    /// <summary>
    /// 是否在线
    /// </summary>
    public bool IsOnline => true;

    /// <summary>
    /// 用户缩写（用于头像显示）
    /// </summary>
    public string UserInitials => "MO";

    // TODO: 未来可扩展为从 IHttpContextAccessor 或认证系统获取真实用户信息
}
