namespace MoLibrary.RegisterCentre.Interfaces;

/// <summary>
/// 注册中心预定义信息提供者接口
/// </summary>
public interface IRegisterCentreInfoProvider
{
    /// <summary>
    /// 获取所有领域信息
    /// </summary>
    /// <returns>所有领域信息列表</returns>
    Task<List<DomainInfo>> GetAllDomainsAsync();
    
    /// <summary>
    /// 获取所有预加载的服务信息
    /// </summary>
    /// <returns>所有预加载的服务信息列表</returns>
    Task<List<ServiceInfo>> GetPreloadedServicesAsync();
}

/// <summary>
/// 领域信息
/// </summary>
public class DomainInfo
{
    /// <summary>
    /// 领域名称
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// 领域显示名称
    /// </summary>
    public string? DisplayName { get; set; }
    
    /// <summary>
    /// 领域描述
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// 服务信息
/// </summary>
public class ServiceInfo
{
    /// <summary>
    /// 应用ID
    /// </summary>
    public required string AppId { get; set; }
    
    /// <summary>
    /// 应用名称
    /// </summary>
    public string? AppName { get; set; }
}