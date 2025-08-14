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
    private readonly IRegisterCentreClientConnector _connector;

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
        IRegisterCentreClientConnector connector,
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
    
    public virtual async Task<Res> Register(ServiceRegisterInfo req)
    {
        if (req.AppId.IsNullOrWhiteSpace()) 
            return Res.Fail("该微服务未设置APPID，无法注册");

        // 补充来源信息
        if (req.FromClient is null && _accessor.HttpContext?.Connection is { } connection)
        {
            req.FromClient = $"[Remote: {connection.RemoteIpAddress}:{connection.RemotePort}][Local: {connection.LocalIpAddress}:{connection.LocalPort}]";
        }
        
        if (req.FromClient.IsNullOrWhiteSpace())
            return Res.Fail("无法识别服务实例来源");

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
                HeartbeatCount = 0
            };
        }

        return Res.Ok();
    }

    public virtual async Task<Res<ServiceHeartbeatResponse>> Heartbeat(ServiceHeartbeat req)
    {
        if (req.AppId.IsNullOrWhiteSpace())
            return "未提供AppId";
            
        // 补充来源信息
        if (req.FromClient is null && _accessor.HttpContext?.Connection is { } connection)
        {
            req.FromClient = $"[Remote: {connection.RemoteIpAddress}:{connection.RemotePort}][Local: {connection.LocalIpAddress}:{connection.LocalPort}]";
        }
        
        if (req.FromClient.IsNullOrWhiteSpace())
            return "无法识别服务实例来源";
        
        var response = new ServiceHeartbeatResponse { RequireReRegister = false };
        
        // 查找服务
        if (!Services.TryGetValue(req.AppId, out var service))
        {
            response.RequireReRegister = true;
            response.Message = "服务未注册";
            return response;
        }
        
        // 查找实例
        var instanceId = req.FromClient!;
        if (!service.Instances.TryGetValue(instanceId, out var instance))
        {
            response.RequireReRegister = true;
            response.Message = "实例未注册";
            return response;
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
            instance.Status = ServiceStatus.Running;
            instance.LastHeartbeatTime = DateTime.Now;
            instance.HeartbeatCount++;
            response.Message = "心跳成功";
        }
        
        return response;
    }

    public virtual async Task<Res<List<RegisteredServiceStatus>>> GetServicesStatus()
    {
        return Services.Values.ToList();
    }

    public virtual async Task<Res> UnregisterAll()
    {
        Services.Clear();
        return Res.Ok();
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
        var timeout = TimeSpan.FromMilliseconds(option.ServerHeartbeatTimeout);
        var now = DateTime.Now;
        
        foreach (var service in Services.Values)
        {
            foreach (var instance in service.Instances.Values)
            {
                if (instance.Status == ServiceStatus.Running)
                {
                    if (now - instance.LastHeartbeatTime > timeout)
                    {
                        instance.Status = ServiceStatus.Offline;
                    }
                }
            }
        }
    }
}