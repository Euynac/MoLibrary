namespace Monica.SignalR.Models;

/// <summary>
/// SignalR server group information
/// </summary>
public class SignalRServerGroupInfo
{

    /// <summary>
    /// Group source Hub class name
    /// </summary>
    public required string Source { get; set; } 

    /// <summary>
    /// Group Hub routing
    /// </summary>
    public required string Route { get; set; }

    /// <summary>
    /// Group method list
    /// </summary>
    public List<SignalRServerMethodInfo> Methods { get; set; } = [];
}


/// <summary>
/// SignalR server method information
/// </summary>
public class SignalRServerMethodInfo
{
    /// <summary>
    /// Method description
    /// </summary>
    public string Desc { get; set; } = string.Empty;

    /// <summary>
    /// method name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Method parameter list
    /// </summary>
    public List<SignalRMethodParameter> Args { get; set; } = [];

}

/// <summary>
/// SignalR method parameter information
/// </summary>
public class SignalRMethodParameter
{
    /// <summary>
    /// Parameter type name
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Parameter name
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// SignalR connection user information
/// </summary>
public class SignalRConnectedUserInfo
{
    /// <summary>
    /// Connection ID
    /// </summary>
    public required string ConnectionId { get; set; }

    /// <summary>
    /// connection time
    /// </summary>
    public DateTime ConnectionTime { get; set; }

    /// <summary>
    /// User Claims Information
    /// </summary>
    public Dictionary<string, string> Claims { get; set; } = [];

    /// <summary>
    /// Has it been certified?
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// Username (extracted from Claims)
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// User ID (extracted from Claims)
    /// </summary>
    public string? UserId { get; set; }
} 