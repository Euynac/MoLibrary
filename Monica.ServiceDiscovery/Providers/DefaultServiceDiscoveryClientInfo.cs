using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using Monica.Modules;
using Monica.ServiceDiscovery.Abstractions;
using Monica.ServiceDiscovery.Models;

namespace Monica.ServiceDiscovery.Providers;

/// <summary>
/// 默认的注册中心客户端信息实现
/// 从ModuleServiceDiscoveryOption读取配置，并提供智能的自动检测回退值
/// </summary>
public class DefaultServiceDiscoveryClientInfo(
    IOptions<ModuleServiceDiscoveryOption> options,
    IServerAddressesFeature? serverAddressesFeature = null) : IServiceDiscoveryClientInfo
{
    private readonly ModuleServiceDiscoveryOption _options = options.Value;

    // 延迟初始化基础服务信息（避免重复执行昂贵操作）
    private readonly Lazy<InstanceState> _baseServiceInfo = new(
        () => BuildBaseServiceInfo(options.Value));

    // 线程安全的本地状态管理（类似 LeaderElectionService 模式）
    private readonly object _stateLock = new();
    private DateTime? _registrationTime;
    private DateTime? _lastHeartbeatTime;

    public DateTime? RegistrationTime
    {
        get
        {
            lock (_stateLock)
            {
                return _registrationTime;
            }
        }
    }

    public DateTime? LastHeartbeatTime
    {
        get
        {
            lock (_stateLock)
            {
                return _lastHeartbeatTime;
            }
        }
    }

    public void SetRegistrationTime(DateTime time)
    {
        lock (_stateLock)
        {
            // 仅在首次注册时设置（后续不再更改）
            _registrationTime ??= time;
        }
    }

    public void UpdateLastHeartbeatTime(DateTime time)
    {
        lock (_stateLock)
        {
            _lastHeartbeatTime = time;
        }
    }

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
    /// Builds the base instance state and normalizes persisted timestamps to UTC.
    /// </summary>
    private static InstanceState BuildBaseServiceInfo(ModuleServiceDiscoveryOption options)
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
            BuildTime = ResolveBuildTimeUtc(options.BuildTime, entryAssembly),
            AssemblyVersion = options.AssemblyVersion ?? GetAssemblyVersion(entryAssembly),
            ReleaseVersion = options.ReleaseVersion,
            DependentSubDomains = options.DependentSubDomains,
            RegistrationTime = DateTime.MinValue,  // 将在 CloneInstanceState 中使用本地状态覆盖
            LastHeartbeatTime = DateTime.MinValue   // 将在 CloneInstanceState 中使用本地状态覆盖
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
    /// Resolves the configured build time or falls back to the assembly file timestamp in UTC.
    /// </summary>
    private static DateTime ResolveBuildTimeUtc(DateTime? configuredBuildTime, Assembly? assembly)
    {
        return configuredBuildTime is { } value
            ? NormalizeUtc(value)
            : GetBuildTimeUtc(assembly);
    }

    /// <summary>
    /// Extracts the build time from the assembly file last write timestamp in UTC.
    /// </summary>
    private static DateTime GetBuildTimeUtc(Assembly? assembly)
    {
        if (assembly == null) return DateTime.MinValue;

        try
        {
            var location = assembly.Location;
            if (string.IsNullOrEmpty(location)) return DateTime.MinValue;

            return File.GetLastWriteTimeUtc(location);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    /// <summary>
    /// Normalizes configured values so the stored timestamp is always UTC.
    /// </summary>
    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime()
        };
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
    private InstanceState CloneInstanceState(InstanceState original)
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
            RegistrationTime = RegistrationTime ?? DateTime.MinValue,  // 使用本地状态
            LastHeartbeatTime = LastHeartbeatTime ?? DateTime.MinValue,  // 使用本地状态
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
            instanceState.Metadata[IServiceDiscoveryClientInfo.LISTENING_ADDRESS_METADATA_KEY] = addresses;
        }
    }
}
