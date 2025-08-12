namespace MoLibrary.RegisterCentre.Models;

public class ServiceHeartbeatResponse
{
    /// <summary>是否需要重新注册</summary>
    public bool RequireReRegister { get; set; }
    
    /// <summary>响应消息</summary>
    public string? Message { get; set; }
}