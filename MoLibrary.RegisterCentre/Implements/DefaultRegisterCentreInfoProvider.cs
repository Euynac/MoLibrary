using System.ComponentModel;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Implements;

/// <summary>
/// 默认注册中心预定义信息提供者实现
/// </summary>
public class DefaultRegisterCentreInfoProvider(IRegisterCentreServer? registerCentreServer = null) : IRegisterCentreInfoProvider
{
    /// <summary>
    /// 获取所有领域信息（默认实现通过IRegisterCentreServer获取已注册的DomainName列表）
    /// </summary>
    /// <returns>所有领域信息列表</returns>
    public virtual async Task<List<DomainInfo>> GetAllDomainsAsync()
    {
        if (registerCentreServer == null)
            return [];
            
        try
        {
            var servicesResult = await registerCentreServer.GetServicesStatus();
            if (servicesResult.IsFailed(out _, out var services))
                return [];
            
            var domainNames = services
                .Where(s => !string.IsNullOrWhiteSpace(s.DomainName))
                .Select(s => s.DomainName!)
                .Distinct()
                .ToList();
            
            return domainNames.Select(name => new DomainInfo
            {
                Name = name,
                DisplayName = name,
                Description = $"领域: {name}"
            }).ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// 获取所有预加载的服务信息（默认实现返回空列表）
    /// </summary>
    /// <returns>所有预加载的服务信息列表</returns>
    public virtual Task<List<PredefinedServiceInfo>> GetPreloadedServicesAsync()
    {
        return Task.FromResult(new List<PredefinedServiceInfo>());
    }

    /// <summary>
    /// 从枚举类型生成领域信息列表（支持Flags枚举）
    /// </summary>
    /// <typeparam name="TEnum">枚举类型</typeparam>
    /// <returns>领域信息列表</returns>
    public static List<DomainInfo> GenerateDomainsFromEnum<TEnum>()
        where TEnum : struct, Enum
    {
        var domainInfos = new List<DomainInfo>();
        var flagValues = Enum.GetValues<TEnum>();

        foreach (var flagValue in flagValues)
        {
            // 跳过None值（通常为0）
            if (Convert.ToInt32(flagValue) == 0) continue;
            var name = flagValue.ToString();
            var displayName = name;

            // 尝试获取Description属性
            var field = typeof(TEnum).GetField(name);
            if (field != null)
            {
                var descriptionAttr = field.GetCustomAttributes(typeof(DescriptionAttribute), false)
                    .OfType<DescriptionAttribute>()
                    .FirstOrDefault();
                if (descriptionAttr != null)
                {
                    displayName = descriptionAttr.Description;
                }
            }

            domainInfos.Add(new DomainInfo
            {
                Name = name,
                DisplayName = displayName
            });
        }

        return domainInfos;
    }
}