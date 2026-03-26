namespace Monica.RegisterCentre.Models;

public enum ServiceStatus
{
    /// <summary>运行中</summary>
    Running,

    /// <summary>更新中</summary>
    Updating,

    /// <summary>离线</summary>
    Offline,

    /// <summary>异常</summary>
    Error,

    /// <summary>不健康（心跳超时但未达到离线阈值）</summary>
    Unhealthy
}