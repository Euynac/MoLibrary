namespace Monica.SignalR.Models;

/// <summary>
/// Describes a single SignalR hub method parameter.
/// </summary>
public sealed class SignalRHubParameterInfo
{
    /// <summary>
    /// Gets or sets the CLR parameter type name.
    /// </summary>
    public string TypeName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameter name.
    /// </summary>
    public string Name { get; init; } = string.Empty;
}
