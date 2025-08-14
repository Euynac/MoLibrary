using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.RegisterCentre.Modules;

namespace MoLibrary.RegisterCentre.Implements;

/// <summary>
/// 注册中心客户端抽象基类
/// </summary>
public abstract class RegisterCentreClientBase(IOptions<ModuleRegisterCentreOption> options, IServerAddressesFeature? serverAddressesFeature = null) : IRegisterCentreClient
{
    protected readonly ModuleRegisterCentreOption _options = options.Value;

    /// <summary>
    /// 获取当前微服务状态
    /// </summary>
    /// <param name="isHeartbeatInfo"></param>
    /// <returns></returns>
    public ServiceRegisterInfo GetServiceStatus(bool isHeartbeatInfo = false)
    {
        var serviceInfo = GetBaseServiceInfo();
        if (isHeartbeatInfo)
        {
            return serviceInfo;
        }
        // 添加环境变量元数据
        AddEnvironmentVariablesMetadata(serviceInfo);
        
        // 添加监听地址元数据
        AddListeningAddressesMetadata(serviceInfo);
        
        return serviceInfo;
    }

    /// <summary>
    /// 获取基础服务信息，由子类实现
    /// </summary>
    /// <returns></returns>
    protected abstract ServiceRegisterInfo GetBaseServiceInfo();

    /// <summary>
    /// 添加环境变量作为元数据
    /// </summary>
    /// <param name="serviceInfo"></param>
    private void AddEnvironmentVariablesMetadata(ServiceRegisterInfo serviceInfo)
    {
        foreach (var envKey in _options.MetadataEnvironmentVariables)
        {
            var envValue = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrEmpty(envValue))
            {
                serviceInfo.Metadata[envKey] = envValue;
            }
        }
    }

    /// <summary>
    /// 添加监听地址作为元数据
    /// </summary>
    /// <param name="serviceInfo"></param>
    private void AddListeningAddressesMetadata(ServiceRegisterInfo serviceInfo)
    {
        if (_options.IncludeListeningAddresses && 
            serverAddressesFeature?.Addresses != null && 
            serverAddressesFeature.Addresses.Any())
        {
            var addresses = string.Join(";", serverAddressesFeature.Addresses);
            serviceInfo.Metadata[IRegisterCentreClient.ListeningAddressMetadataKey] = addresses;
        }
    }
    
    /// <summary>
    /// 根据Flags枚举获得依赖子域
    /// </summary>
    /// <typeparam name="TEnum">Flags枚举类型</typeparam>
    /// <param name="domainFlags">领域标志枚举实例</param>
    public List<string> SetDependentSubDomains<TEnum>(TEnum domainFlags) 
        where TEnum : struct, Enum
    {
        if (!typeof(TEnum).IsDefined(typeof(FlagsAttribute), false))
        {
            throw new ArgumentException("枚举类型必须标记为Flags", nameof(domainFlags));
        }

        var domains = new List<string>();

        var flagValues = Enum.GetValues<TEnum>();
        
        foreach (var flagValue in flagValues)
        {
            // 跳过None值（通常为0）
            if (Convert.ToInt32(flagValue) == 0) continue;
            
            // 检查是否包含该标志
            if (domainFlags.HasFlag(flagValue))
            {
                domains.Add(flagValue.ToString());
            }
        }

        return domains;
    }
}