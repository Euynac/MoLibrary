namespace MoLibrary.RegisterCentre.Models;

public enum ServiceStatus
{
    /// <summary>运行中</summary>
    Running = 0,
    
    /// <summary>更新中</summary>
    Updating = 1,
    
    /// <summary>离线</summary>
    Offline = 2,
    
    /// <summary>异常</summary>
    Error = 3
}