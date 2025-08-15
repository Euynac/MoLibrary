using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.FrameworkUI.UIRegisterCentre.Services;

public class RegisterCentreService(
    ILogger<RegisterCentreService> logger,
    IServiceProvider serviceProvider)
{
    private static readonly Dictionary<string, string> _domainColors = new();
    private static List<DomainInfo> _cachedDomains = [];
    public async Task<Res<List<RegisteredServiceStatus>>> GetServicesStatusAsync()
    {
        try
        {
            var registerCentreServer = serviceProvider.GetService<IRegisterCentreServer>();
            if (registerCentreServer == null)
            {
                return "当前服务未配置为注册中心服务端";
            }

            return await registerCentreServer.GetServicesStatus();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取服务状态失败");
            return $"获取服务状态失败: {ex.Message}";
        }
    }

    public async Task<Res> UnregisterAllAsync()
    {
        try
        {
            var registerCentreServer = serviceProvider.GetService<IRegisterCentreServer>();
            if (registerCentreServer == null)
            {
                return "当前服务未配置为注册中心服务端";
            }

            return await registerCentreServer.UnregisterAll();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "清空所有注册失败");
            return $"清空所有注册失败: {ex.Message}";
        }
    }

    public async Task<Res> RegisterServiceAsync(ServiceRegisterInfo info)
    {
        try
        {
            var registerCentreServer = serviceProvider.GetService<IRegisterCentreServer>();
            if (registerCentreServer == null)
            {
                return "当前服务未配置为注册中心服务端";
            }

            return await registerCentreServer.Register(info);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "注册服务失败");
            return $"注册服务失败: {ex.Message}";
        }
    }

    public async Task<Res> TestReconnectAsync()
    {
        try
        {
            var connector = serviceProvider.GetService<IRegisterCentreServerConnector>();
            var client = serviceProvider.GetService<IRegisterCentreClient>();
            
            if (connector == null || client == null)
            {
                return "当前服务未配置为注册中心客户端";
            }

            return await connector.Register(client.GetServiceStatus());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "测试重连失败");
            return $"测试重连失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 获取合并的服务状态列表（包含预定义服务和已注册服务）
    /// </summary>
    /// <returns>合并后的服务状态列表</returns>
    public async Task<Res<List<RegisteredServiceStatus>>> GetMergedServicesStatusAsync()
    {
        try
        {
            var registerCentreServer = serviceProvider.GetService<IRegisterCentreServer>();
            var infoProvider = serviceProvider.GetService<IRegisterCentreInfoProvider>();

            // 获取已注册的服务状态
            List<RegisteredServiceStatus> registeredServices = [];
            if (registerCentreServer != null)
            {
                if (!(await registerCentreServer.GetServicesStatus()).IsFailed(out _, out var data))
                {
                    registeredServices = data ?? [];
                }
            }

            // 获取预定义的服务信息
            List<PredefinedServiceInfo> preloadedServices = [];
            if (infoProvider != null)
            {
                preloadedServices = await infoProvider.GetPreloadedServicesAsync();
            }

            // 合并服务列表
            var mergedServices = new List<RegisteredServiceStatus>(registeredServices);

            // 检查预定义服务是否已在注册服务中存在，如果不存在则添加
            foreach (var preloadedService in preloadedServices)
            {
                var existingService = registeredServices.FirstOrDefault(r => r.AppId == preloadedService.AppId);
                if (existingService == null)
                {
                    // 创建一个离线状态的服务条目
                    mergedServices.Add(new RegisteredServiceStatus
                    {
                        AppId = preloadedService.AppId,
                        AppName = preloadedService.AppName ?? preloadedService.AppId,
                        // 其他属性保持默认值，Instances为空字典表示离线状态
                    });
                }
            }

            return mergedServices;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取合并服务状态失败");
            return $"获取合并服务状态失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 获取域信息并初始化颜色分配
    /// </summary>
    /// <returns>域信息列表</returns>
    public async Task<Res<List<DomainInfo>>> GetDomainsWithColorsAsync()
    {
        try
        {
            var infoProvider = serviceProvider.GetService<IRegisterCentreInfoProvider>();
            if (infoProvider == null)
            {
                return "当前服务未配置IRegisterCentreInfoProvider";
            }

            var domains = await infoProvider.GetAllDomainsAsync();
           
            // 检查是否需要重新初始化颜色分配
            if (_cachedDomains.Count != domains.Count || 
                !domains.All(d => _cachedDomains.Any(c => c.Name == d.Name)))
            {
                _cachedDomains = domains.ToList();
                InitializeDomainColors(_cachedDomains);
                logger.LogInformation("重新初始化域颜色分配，共 {Count} 个域", domains.Count);
            }

            return domains.ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取域信息失败");
            return $"获取域信息失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 获取指定域的颜色
    /// </summary>
    /// <param name="domainName">域名称</param>
    /// <returns>域对应的颜色，如果域不存在则返回默认颜色</returns>
    public string GetDomainColor(string domainName)
    {
        if (string.IsNullOrEmpty(domainName))
        {
            return "#666666"; // 默认灰色
        }

        return _domainColors.GetValueOrDefault(domainName, "#666666");
    }

    /// <summary>
    /// 获取所有域颜色映射
    /// </summary>
    /// <returns>域名到颜色的映射字典</returns>
    public Dictionary<string, string> GetAllDomainColors()
    {
        return _domainColors;
    }

    /// <summary>
    /// 初始化域颜色分配
    /// </summary>
    /// <param name="domains">域信息列表</param>
    private static void InitializeDomainColors(List<DomainInfo> domains)
    {
        _domainColors.Clear();

        if (!domains.Any()) return;

        var colors = GenerateDistinctColors(domains.Count);
        for (var i = 0; i < domains.Count; i++)
        {
            _domainColors[domains[i].Name] = colors[i];
        }
    }

    /// <summary>
    /// 生成视觉上区分度高的颜色
    /// </summary>
    /// <param name="count">需要生成的颜色数量</param>
    /// <returns>颜色列表</returns>
    private static List<string> GenerateDistinctColors(int count)
    {
        var colors = new List<string>();
        if (count <= 0) return colors;

        var hueStep = 360.0 / count;

        for (var i = 0; i < count; i++)
        {
            var hue = (i * hueStep) % 360;
            var saturation = 65 + (i % 3) * 15; // 65-95%
            var lightness = 50 + (i % 2) * 10; // 50-60%
            colors.Add($"hsl({hue:F0}, {saturation}%, {lightness}%)");
        }

        return colors;
    }

    /// <summary>
    /// 获取指定域的相关微服务列表
    /// </summary>
    /// <param name="domainName">域名称</param>
    /// <returns>该域相关的微服务列表</returns>
    public async Task<Res<List<RegisteredServiceStatus>>> GetDomainRelatedServicesAsync(string domainName)
    {
        try
        {
            if (string.IsNullOrEmpty(domainName))
            {
                return "域名称不能为空";
            }

            var servicesResult = await GetMergedServicesStatusAsync();
            if (servicesResult.IsFailed(out var error, out var services))
            {
                return error;
            }

            var domainServices = services
                .Where(s => string.Equals(s.DomainName, domainName, StringComparison.OrdinalIgnoreCase) ||
                           (s.DependentSubDomains?.Contains(domainName, StringComparer.OrdinalIgnoreCase) == true))
                .ToList();

            return domainServices;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取域相关服务失败");
            return $"获取域相关服务失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 获取域详细信息（包含相关微服务）
    /// </summary>
    /// <param name="domainName">域名称</param>
    /// <returns>域详细信息</returns>
    public async Task<Res<DomainDetailInfo>> GetDomainDetailAsync(string domainName)
    {
        try
        {
            if (string.IsNullOrEmpty(domainName))
            {
                return "域名称不能为空";
            }

            var domainsResult = await GetDomainsWithColorsAsync();
            if (domainsResult.IsFailed(out var error, out var domains))
            {
                return error;
            }

            var domain = domains.FirstOrDefault(d => string.Equals(d.Name, domainName, StringComparison.OrdinalIgnoreCase));
            if (domain == null)
            {
                return $"未找到域: {domainName}";
            }

            var servicesResult = await GetDomainRelatedServicesAsync(domainName);
            if (servicesResult.IsFailed(out var servicesError, out var services))
            {
                return servicesError;
            }

            var domainDetail = new DomainDetailInfo
            {
                Domain = domain,
                RelatedServices = services,
                Color = GetDomainColor(domainName)
            };

            return domainDetail;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取域详细信息失败");
            return $"获取域详细信息失败: {ex.Message}";
        }
    }
}

/// <summary>
/// 域详细信息
/// </summary>
public class DomainDetailInfo
{
    /// <summary>
    /// 域信息
    /// </summary>
    public required DomainInfo Domain { get; set; }

    /// <summary>
    /// 相关微服务列表
    /// </summary>
    public List<RegisteredServiceStatus> RelatedServices { get; set; } = [];

    /// <summary>
    /// 域颜色
    /// </summary>
    public string Color { get; set; } = "#666666";
}