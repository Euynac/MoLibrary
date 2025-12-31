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
    private readonly Lazy<InstanceState> _baseServiceInfo = new(
        () => BuildBaseServiceInfo(options.Value));

    public InstanceState GetServiceStatus(bool isHeartbeatInfo = false)
    {
        // 克隆基础信息以避免修改缓存的实例
        var instanceState = CloneInstanceState(_baseServiceInfo.Value);

        if (isHeartbeatInfo)
        {
            return instanceState;
        }

        // 为完整注册添加元数据（心跳时不添加）
        AddEnvironmentVariablesMetadata(instanceState);
        AddListeningAddressesMetadata(instanceState);

        return instanceState;
    }

    /// <summary>
    /// 从配置选项构建基础实例状态信息，使用自动检测作为回退
    /// </summary>
    private static InstanceState BuildBaseServiceInfo(ModuleRegisterCentreOption options)
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        var assemblyName = entryAssembly?.GetName().Name ?? "Unknown";

        return new InstanceState
        {
            ServiceName = options.AppId ?? assemblyName,
            InstanceId = options.FromInstance ?? GenerateFromInstance(),
            AppName = options.AppName ?? assemblyName,
            ProjectName = options.ProjectName ?? assemblyName,
            DomainName = options.DomainName,
            BuildTime = options.BuildTime ?? GetBuildTime(entryAssembly),
            AssemblyVersion = options.AssemblyVersion ?? GetAssemblyVersion(entryAssembly),
            ReleaseVersion = options.ReleaseVersion,
            DependentSubDomains = options.DependentSubDomains,
            RegistrationTime = DateTime.MinValue,
            LastHeartbeatTime = DateTime.MinValue
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
    /// 克隆 InstanceState 以避免修改缓存的实例
    /// </summary>
    private static InstanceState CloneInstanceState(InstanceState original)
    {
        return new InstanceState
        {
            ServiceName = original.ServiceName,
            InstanceId = original.InstanceId,
            AppName = original.AppName,
            ProjectName = original.ProjectName,
            DomainName = original.DomainName,
            BuildTime = original.BuildTime,
            AssemblyVersion = original.AssemblyVersion,
            ReleaseVersion = original.ReleaseVersion,
            DependentSubDomains = original.DependentSubDomains?.ToList(),
            RegistrationTime = original.RegistrationTime,
            LastHeartbeatTime = original.LastHeartbeatTime,
            Metadata = new Dictionary<string, string>() // 从空元数据开始
        };
    }

    /// <summary>
    /// 根据配置将环境变量添加为元数据
    /// </summary>
    private void AddEnvironmentVariablesMetadata(InstanceState instanceState)
    {
        foreach (var envKey in _options.MetadataEnvironmentVariables)
        {
            var envValue = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrEmpty(envValue))
            {
                instanceState.Metadata[envKey] = envValue;
            }
        }
    }

    /// <summary>
    /// 如果配置启用，将监听地址添加为元数据
    /// </summary>
    private void AddListeningAddressesMetadata(InstanceState instanceState)
    {
        if (_options.IncludeListeningAddresses &&
            serverAddressesFeature?.Addresses != null &&
            serverAddressesFeature.Addresses.Any())
        {
            var addresses = string.Join(";", serverAddressesFeature.Addresses);
            instanceState.Metadata[IRegisterCentreClientInfo.LISTENING_ADDRESS_METADATA_KEY] = addresses;
        }
    }
}
