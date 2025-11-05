using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MoLibrary.RegisterCentre.Interfaces;
using MoLibrary.RegisterCentre.Models;
using MoLibrary.RegisterCentre.Modules;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.RegisterCentre.Implements;

/// <summary>
/// 内存注册中心实现，支持多实例管理
/// </summary>
public class MemoryProviderForRegisterCentre : IRegisterCentreServer
{
    private readonly IHttpContextAccessor _accessor;
    private readonly IRegisterCentreServerInvocationConnector _connector;

    /// <summary>
    /// 服务字典（Key: AppId, Value: RegisteredServiceStatus）
    /// </summary>
    protected static readonly ConcurrentDictionary<string, RegisteredServiceStatus> Services = new();
    
    /// <summary>
    /// 心跳超时检查定时器
    /// </summary>
    private static Timer? _heartbeatCheckTimer;
    
    /// <summary>
    /// 配置选项实例（静态方法需要）
    /// </summary>
    private static ModuleRegisterCentreOption? _staticOption;
    
    public MemoryProviderForRegisterCentre(
        IHttpContextAccessor accessor,
        IRegisterCentreServerInvocationConnector connector,
        IOptions<ModuleRegisterCentreOption> options)
    {
        _accessor = accessor;
        _connector = connector;
        var option = options.Value;

        // 初始化定时器（单例模式，只初始化一次）
        if (_staticOption == null)
        {
            _staticOption = option;
            var checkInterval = TimeSpan.FromMilliseconds(option.ServerHeartbeatCheckInterval);
            _heartbeatCheckTimer ??= new Timer(CheckHeartbeatTimeout, null, checkInterval, checkInterval);
        }
    }
    
    public virtual Task<Res> Register(ServiceRegisterInfo req)
    {
        if (req.AppId.IsNullOrWhiteSpace())
            return Task.FromResult(Res.Fail("该微服务未设置APPID，无法注册"));

        // 补充来源信息
        if (req.FromClient is null && _accessor.HttpContext?.Connection is { } connection)
        {
            req.FromClient = $"[Remote: {connection.RemoteIpAddress}:{connection.RemotePort}][Local: {connection.LocalIpAddress}:{connection.LocalPort}]";
        }

        if (req.FromClient.IsNullOrWhiteSpace())
            return Task.FromResult(Res.Fail("无法识别服务实例来源"));

        var service = Services.GetOrAdd(req.AppId, _ => new RegisteredServiceStatus
        {
            AppId = req.AppId,
            AppName = req.AppName,
            DomainName = req.DomainName,
            ProjectName = req.ProjectName,
            DependentSubDomains = req.DependentSubDomains
        });

        // 更新服务基本信息
        service.AppName = req.AppName;
        service.DomainName = req.DomainName;
        service.ProjectName = req.ProjectName;
        service.DependentSubDomains = req.DependentSubDomains;

        // 添加或更新实例
        var instanceId = req.FromClient!;
        if (service.Instances.TryGetValue(instanceId, out var instance))
        {
            // 更新现有实例
            instance.RegisterInfo = req;
            instance.Status = ServiceStatus.Running;
            instance.LastHeartbeatTime = DateTime.Now;
        }
        else
        {
            // 添加新实例
            service.Instances[instanceId] = new ServiceInstance
            {
                InstanceId = instanceId,
                RegisterInfo = req,
                Status = ServiceStatus.Running,
                LastHeartbeatTime = DateTime.Now,
                RegistrationTime = DateTime.Now,
                HeartbeatCount = 0,
                IsLeader = false // 初始化时默认不是领导者
            };
        }

        // 触发领导者选举（如果启用）
        if (_staticOption?.EnableLeaderElection ?? true)
        {
            PerformLeaderElection(service);
        }

        return Task.FromResult(Res.Ok());
    }

    public virtual Task<Res<ServiceHeartbeatResponse>> Heartbeat(ServiceHeartbeat req)
    {
        if (req.AppId.IsNullOrWhiteSpace())
            return Task.FromResult<Res<ServiceHeartbeatResponse>>("未提供AppId");

        // 补充来源信息
        if (req.FromClient is null && _accessor.HttpContext?.Connection is { } connection)
        {
            req.FromClient = $"[Remote: {connection.RemoteIpAddress}:{connection.RemotePort}][Local: {connection.LocalIpAddress}:{connection.LocalPort}]";
        }

        if (req.FromClient.IsNullOrWhiteSpace())
            return Task.FromResult<Res<ServiceHeartbeatResponse>>("无法识别服务实例来源");

        var response = new ServiceHeartbeatResponse { RequireReRegister = false };

        // 查找服务
        if (!Services.TryGetValue(req.AppId, out var service))
        {
            response.RequireReRegister = true;
            response.Message = "服务未注册";
            return Task.FromResult<Res<ServiceHeartbeatResponse>>(response);
        }

        // 查找实例
        var instanceId = req.FromClient!;
        if (!service.Instances.TryGetValue(instanceId, out var instance))
        {
            response.RequireReRegister = true;
            response.Message = "实例未注册";
            return Task.FromResult<Res<ServiceHeartbeatResponse>>(response);
        }

        // 检查版本信息是否一致
        var registerInfo = instance.RegisterInfo;
        bool versionChanged = registerInfo.BuildTime != req.BuildTime ||
                             registerInfo.AssemblyVersion != req.AssemblyVersion ||
                             registerInfo.ReleaseVersion != req.ReleaseVersion;

        if (versionChanged)
        {
            instance.Status = ServiceStatus.Updating;
            response.RequireReRegister = true;
            response.Message = "检测到版本变化，需要重新注册";
        }
        else
        {
            // 更新心跳信息
            // 无论实例之前是什么状态（Running/Unhealthy/Offline），成功的心跳都会立即恢复到Running状态
            instance.Status = ServiceStatus.Running;
            instance.LastHeartbeatTime = DateTime.Now;
            instance.HeartbeatCount++;
            response.Message = "心跳成功";
        }

        return Task.FromResult<Res<ServiceHeartbeatResponse>>(response);
    }

    public virtual Task<Res<LeaderStatusResponse>> GetLeaderStatus(LeaderStatusRequest req)
    {
        if (req.AppId.IsNullOrWhiteSpace())
            return Task.FromResult<Res<LeaderStatusResponse>>("未提供AppId");

        // 补充来源信息
        if (req.FromClient is null && _accessor.HttpContext?.Connection is { } connection)
        {
            req.FromClient = $"[Remote: {connection.RemoteIpAddress}:{connection.RemotePort}][Local: {connection.LocalIpAddress}:{connection.LocalPort}]";
        }

        if (req.FromClient.IsNullOrWhiteSpace())
            return Task.FromResult<Res<LeaderStatusResponse>>("无法识别服务实例来源");

        var response = new LeaderStatusResponse();

        // 查找服务
        if (!Services.TryGetValue(req.AppId, out var service))
        {
            response.Status = LeaderStatus.Looking;
            response.Message = "服务未注册";
            response.RunningInstanceCount = 0;
            return Task.FromResult<Res<LeaderStatusResponse>>(response);
        }

        // 获取所有符合条件的实例（Running和Unhealthy状态）
        var eligibleInstances = service.Instances.Values
            .Where(i => i.Status == ServiceStatus.Running || i.Status == ServiceStatus.Unhealthy)
            .ToList();

        response.RunningInstanceCount = eligibleInstances.Count;

        // 如果没有符合条件的实例
        if (eligibleInstances.Count == 0)
        {
            response.Status = LeaderStatus.Looking;
            response.Message = "当前没有运行中的实例";
            return Task.FromResult<Res<LeaderStatusResponse>>(response);
        }

        // 查找当前请求的实例
        var instanceId = req.FromClient!;
        var currentInstance = service.Instances.GetValueOrDefault(instanceId);

        // 查找领导者
        var leader = eligibleInstances.FirstOrDefault(i => i.IsLeader);

        if (leader == null)
        {
            // 没有领导者，返回Looking状态
            response.Status = LeaderStatus.Looking;
            response.Message = "正在进行领导者选举";
            return Task.FromResult<Res<LeaderStatusResponse>>(response);
        }

        // 设置领导者信息
        response.LeaderInstanceId = leader.InstanceId;
        response.LeaderRegistrationTime = leader.RegistrationTime;

        // 判断当前实例的状态
        if (currentInstance != null && currentInstance.IsLeader)
        {
            response.Status = LeaderStatus.Leader;
            response.Message = "当前实例是领导者";
        }
        else
        {
            response.Status = LeaderStatus.Follower;
            response.Message = $"当前实例是跟随者，领导者是 {leader.InstanceId}";
        }

        return Task.FromResult<Res<LeaderStatusResponse>>(response);
    }

    public virtual Task<Res<List<RegisteredServiceStatus>>> GetServicesStatus()
    {
        return Task.FromResult<Res<List<RegisteredServiceStatus>>>(Services.Values.ToList());
    }

    public virtual Task<Res> UnregisterAll()
    {
        Services.Clear();
        return Task.FromResult(Res.Ok());
    }

    public virtual async Task<Dictionary<ServiceRegisterInfo, Res<TResponse>>> GetAsync<TResponse>(string callbackUrl)
    {
        var appIds = Services.Keys.ToList();
        var dict = await _connector.GetAsync<TResponse>(appIds, callbackUrl);
        
        var result = new Dictionary<ServiceRegisterInfo, Res<TResponse>>();
        foreach (var (appId, response) in dict)
        {
            if (Services.TryGetValue(appId, out var service))
            {
                // 使用第一个运行中的实例的注册信息
                var runningInstance = service.Instances.Values
                    .FirstOrDefault(i => i.Status == ServiceStatus.Running);
                    
                if (runningInstance != null)
                {
                    result[runningInstance.RegisterInfo] = response;
                }
            }
        }
        
        return result;
    }

    public virtual async Task<Dictionary<ServiceRegisterInfo, Res<TResponse>>> PostAsync<TRequest, TResponse>(string callbackUrl, TRequest req)
    {
        var appIds = Services.Keys.ToList();
        var dict = await _connector.PostAsync<TRequest, TResponse>(appIds, callbackUrl, req);
        
        var result = new Dictionary<ServiceRegisterInfo, Res<TResponse>>();
        foreach (var (appId, response) in dict)
        {
            if (Services.TryGetValue(appId, out var service))
            {
                // 使用第一个运行中的实例的注册信息
                var runningInstance = service.Instances.Values
                    .FirstOrDefault(i => i.Status == ServiceStatus.Running);
                    
                if (runningInstance != null)
                {
                    result[runningInstance.RegisterInfo] = response;
                }
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// 检查心跳超时
    /// </summary>
    private static void CheckHeartbeatTimeout(object? state)
    {
        var option = _staticOption ?? new ModuleRegisterCentreOption();
        var unhealthyThreshold = TimeSpan.FromMilliseconds(option.UnhealthyThreshold);
        var offlineThreshold = TimeSpan.FromMilliseconds(option.OfflineThreshold);
        var expelThreshold = TimeSpan.FromMilliseconds(option.ExpelThreshold);
        var now = DateTime.Now;

        foreach (var service in Services.Values)
        {
            // 1. 检查心跳超时并根据阈值更新实例状态
            var instancesToRemove = new List<string>();

            foreach (var instance in service.Instances.Values)
            {
                // 跳过Error和Updating状态的实例，这些状态由其他逻辑管理
                if (instance.Status == ServiceStatus.Error || instance.Status == ServiceStatus.Updating)
                    continue;

                var timeSinceLastHeartbeat = now - instance.LastHeartbeatTime;

                // 多级健康检查：根据时间阈值进行状态转换
                if (timeSinceLastHeartbeat > expelThreshold)
                {
                    // 超过驱逐阈值：从注册中心移除实例
                    instancesToRemove.Add(instance.InstanceId);
                }
                else if (timeSinceLastHeartbeat > offlineThreshold)
                {
                    // 超过离线阈值：标记为Offline
                    if (instance.Status != ServiceStatus.Offline)
                    {
                        instance.Status = ServiceStatus.Offline;
                        // 如果离线的是领导者，标记为非领导者以触发重新选举
                        if (option.EnableLeaderElection && instance.IsLeader)
                        {
                            instance.IsLeader = false;
                        }
                    }
                }
                else if (timeSinceLastHeartbeat > unhealthyThreshold)
                {
                    // 超过不健康阈值：标记为Unhealthy
                    if (instance.Status == ServiceStatus.Running)
                    {
                        instance.Status = ServiceStatus.Unhealthy;
                        // 注意：根据配置，Unhealthy实例仍可保持领导者身份
                    }
                }
                // 如果未超过任何阈值，保持当前状态（由心跳处理恢复到Running）
            }

            // 2. 移除需要驱逐的实例
            foreach (var instanceId in instancesToRemove)
            {
                service.Instances.Remove(instanceId);
            }

            // 3. 进行领导者选举（如果启用）
            if (option.EnableLeaderElection)
            {
                PerformLeaderElection(service);
            }
        }
    }

    /// <summary>
    /// 为指定服务执行领导者选举
    /// </summary>
    /// <param name="service">服务状态信息</param>
    private static void PerformLeaderElection(RegisteredServiceStatus service)
    {
        // 获取所有符合条件的实例（Running和Unhealthy状态）
        // Unhealthy实例仍可参与领导者选举，只有Offline和Error状态的实例被排除
        var eligibleInstances = service.Instances.Values
            .Where(i => i.Status == ServiceStatus.Running || i.Status == ServiceStatus.Unhealthy)
            .ToList();

        // 如果没有符合条件的实例，清除所有领导者标记
        if (eligibleInstances.Count == 0)
        {
            foreach (var instance in service.Instances.Values)
            {
                instance.IsLeader = false;
            }
            return;
        }

        // 检查是否已有领导者
        var currentLeader = eligibleInstances.FirstOrDefault(i => i.IsLeader);
        if (currentLeader != null)
        {
            // 已有领导者，无需重新选举，但确保其他实例不是领导者
            foreach (var instance in eligibleInstances.Where(i => i != currentLeader))
            {
                instance.IsLeader = false;
            }
            return;
        }

        // 没有领导者，按注册时间排序，选择最早注册的实例为领导者
        var newLeader = eligibleInstances
            .OrderBy(i => i.RegistrationTime)
            .First();

        // 设置新领导者
        newLeader.IsLeader = true;

        // 确保其他实例不是领导者
        foreach (var instance in eligibleInstances.Where(i => i != newLeader))
        {
            instance.IsLeader = false;
        }
    }
}