using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.RegisterCentre.Modules;

namespace MoLibrary.RegisterCentre.Implements;

/// <summary>
/// 默认的注册中心客户端信息实现
/// 从ModuleRegisterCentreOption读取配置，并提供智能的自动检测回退值
/// </summary>
public class DefaultRegisterCentreClientInfo(
    IOptions<ModuleRegisterCentreOption> options,
    IServerAddressesFeature? serverAddressesFeature = null) : IRegisterCentreClientInfo
{
    private readonly ModuleRegisterCentreOption _options = options.Value;

    // 延迟初始化基础服务信息（避免重复执行昂贵操作）
    private readonly Lazy<ServiceRegisterInfo> _baseServiceInfo = new(
        () => BuildBaseServiceInfo(options.Value));

    public ServiceRegisterInfo GetServiceStatus(bool isHeartbeatInfo = false)
    {
        // 克隆基础信息以避免修改缓存的实例
        var serviceInfo = CloneServiceInfo(_baseServiceInfo.Value);

        if (isHeartbeatInfo)
        {
            return serviceInfo;
        }

        // 为完整注册添加元数据（心跳时不添加）
        AddEnvironmentVariablesMetadata(serviceInfo);
        AddListeningAddressesMetadata(serviceInfo);

        return serviceInfo;
    }

    public string GetRegisterCentreAppId()
    {
        // 单实例内存模式返回特殊标识
        if (_options.IsStandaloneMode)
        {
            return "InMemory";
        }

        // 分布式模式必须配置RegisterCentreAppId
        if (string.IsNullOrWhiteSpace(_options.RegisterCentreAppId))
        {
            throw new InvalidOperationException(
                "RegisterCentreAppId must be configured in ModuleRegisterCentreOption when using distributed mode. " +
                "Set it via: ConfigModuleRegisterCentre(o => o.RegisterCentreAppId = \"YourRegistryCentreAppId\")");
        }

        return _options.RegisterCentreAppId;
    }

    /// <summary>
    /// 从配置选项构建基础服务信息，使用自动检测作为回退
    /// </summary>
    private static ServiceRegisterInfo BuildBaseServiceInfo(ModuleRegisterCentreOption options)
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        var assemblyName = entryAssembly?.GetName().Name ?? "Unknown";

        return new ServiceRegisterInfo
        {
            DomainName = options.DomainName,
            AppId = options.AppId ?? assemblyName,
            AppName = options.AppName ?? assemblyName,
            ProjectName = options.ProjectName ?? assemblyName,
            BuildTime = options.BuildTime ?? GetBuildTime(entryAssembly),
            AssemblyVersion = options.AssemblyVersion ?? GetAssemblyVersion(entryAssembly),
            ReleaseVersion = options.ReleaseVersion,
            FromInstance = options.FromInstance ?? GenerateFromInstance(),
            DependentSubDomains = options.DependentSubDomains,
            UpdateTime = DateTime.Now
        };
    }

    /// <summary>
    /// 自动生成FromInstance标识符，格式: hostname:processId
    /// </summary>
    private static string GenerateFromInstance()
    {
        var hostname = Environment.GetEnvironmentVariable("COMPUTERNAME")
                       ?? Environment.GetEnvironmentVariable("HOSTNAME")
                       ?? Environment.MachineName;
        var processId = Environment.ProcessId;
        return $"{hostname}:{processId}";
    }

    /// <summary>
    /// 从程序集文件的最后修改时间提取构建时间
    /// </summary>
    private static DateTime GetBuildTime(Assembly? assembly)
    {
        if (assembly == null) return DateTime.MinValue;

        try
        {
            var location = assembly.Location;
            if (string.IsNullOrEmpty(location)) return DateTime.MinValue;

            return File.GetLastWriteTime(location);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    /// <summary>
    /// 从FileVersionInfo提取程序集版本号
    /// </summary>
    private static string? GetAssemblyVersion(Assembly? assembly)
    {
        if (assembly == null) return null;

        try
        {
            var location = assembly.Location;
            if (string.IsNullOrEmpty(location)) return null;

            var versionInfo = FileVersionInfo.GetVersionInfo(location);
            return versionInfo.ProductVersion;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 克隆ServiceRegisterInfo以避免修改缓存的实例
    /// </summary>
    private static ServiceRegisterInfo CloneServiceInfo(ServiceRegisterInfo original)
    {
        return new ServiceRegisterInfo
        {
            DomainName = original.DomainName,
            AppId = original.AppId,
            AppName = original.AppName,
            ProjectName = original.ProjectName,
            BuildTime = original.BuildTime,
            AssemblyVersion = original.AssemblyVersion,
            ReleaseVersion = original.ReleaseVersion,
            FromInstance = original.FromInstance,
            DependentSubDomains = original.DependentSubDomains?.ToList(),
            UpdateTime = DateTime.Now, // 总是使用当前时间
            Metadata = new Dictionary<string, string>() // 从空元数据开始
        };
    }

    /// <summary>
    /// 根据配置将环境变量添加为元数据
    /// </summary>
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
    /// 如果配置启用，将监听地址添加为元数据
    /// </summary>
    private void AddListeningAddressesMetadata(ServiceRegisterInfo serviceInfo)
    {
        if (_options.IncludeListeningAddresses &&
            serverAddressesFeature?.Addresses != null &&
            serverAddressesFeature.Addresses.Any())
        {
            var addresses = string.Join(";", serverAddressesFeature.Addresses);
            serviceInfo.Metadata[IRegisterCentreClientInfo.LISTENING_ADDRESS_METADATA_KEY] = addresses;
        }
    }
}
