using System.Linq;

namespace MoLibrary.RegisterCentre.Models;

public class RegisteredServiceStatus
{
    /// <summary>服务AppId</summary>
    public required string AppId { get; set; }
    
    /// <summary>服务名称</summary>
    public required string ServiceName { get; set; }
    
    /// <summary>领域名</summary>
    public string? DomainName { get; set; }
    
    /// <summary>项目名</summary>
    public required string ProjectName { get; set; }
    
    /// <summary>服务实例字典（Key: FromClient, Value: ServiceInstance）</summary>
    public Dictionary<string, ServiceInstance> Instances { get; set; } = new();
    
    /// <summary>获取运行中的实例数量</summary>
    public int RunningInstanceCount => 
        Instances.Count(x => x.Value.Status == ServiceStatus.Running);
    
    /// <summary>获取总实例数量</summary>
    public int TotalInstanceCount => Instances.Count;
    
    /// <summary>服务整体状态（基于所有实例状态判断）</summary>
    public ServiceStatus OverallStatus => DetermineOverallStatus();

    /// <summary>
    /// 获取任意一个正常的服务实例信息
    /// </summary>
    /// <returns></returns>
    public ServiceInstance? GetRunningInstanceInfo() => Instances.Values.FirstOrDefault(x => x.Status == ServiceStatus.Running);

    private ServiceStatus DetermineOverallStatus()
    {
        if (!Instances.Any())
            return ServiceStatus.Offline;
            
        var statuses = Instances.Values.Select(x => x.Status).ToList();
        
        if (statuses.Any(s => s == ServiceStatus.Error))
            return ServiceStatus.Error;
            
        if (statuses.Any(s => s == ServiceStatus.Running))
            return ServiceStatus.Running;
            
        if (statuses.Any(s => s == ServiceStatus.Updating))
            return ServiceStatus.Updating;
            
        return ServiceStatus.Offline;
    }
}