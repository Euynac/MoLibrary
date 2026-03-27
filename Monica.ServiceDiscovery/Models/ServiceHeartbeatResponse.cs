namespace Monica.ServiceDiscovery.Models;

public class ServiceHeartbeatResponse
{
    /// <summary>Do you need to re-register?</summary>
    public bool RequireReRegister { get; set; }
    
    /// <summary>response message</summary>
    public string? Message { get; set; }
}