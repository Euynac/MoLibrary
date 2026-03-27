using System.Text.Json.Serialization;
using Monica.Core.JsonSerialization.Converters;

namespace Monica.ServiceDiscovery.Models;

/// <summary>
/// Instance registration status
/// </summary>
public class InstanceState
{
    /// <summary>
    /// Whether to be the leader (updated from memory during heartbeat)
    /// </summary>
    public bool IsLeader { get; set; }

    /// <summary>
    /// Instance status (computed on demand, not persisted)
    /// </summary>
    [JsonIgnore]
    public ServiceStatus Status { get; set; }

    /// <summary>
    /// Service name (corresponding to AppId)
    /// </summary>
    public required string ServiceName { get; set; }

    /// <summary>
    /// Instance ID (corresponding to FromInstance)
    /// </summary>
    public required string InstanceId { get; set; }

    /// <summary>
    /// Microservice display name
    /// </summary>
    public required string AppName { get; set; }

    /// <summary>
    /// Project name
    /// </summary>
    public required string ProjectName { get; set; }

    /// <summary>
    /// Subdomain name
    /// </summary>
    public string? DomainName { get; set; }

    /// <summary>
    /// Microservice build time stored in UTC.
    /// </summary>
    public DateTime BuildTime { get; set; }

    /// <summary>
    /// Microservice assembly version number
    /// </summary>
    public string? AssemblyVersion { get; set; }

    /// <summary>
    /// Microservice release version number
    /// </summary>
    public string? ReleaseVersion { get; set; }

    /// <summary>
    /// Dependent subdomain list
    /// </summary>
    public List<string>? DependentSubDomains { get; set; }

    /// <summary>
    /// Registration time
    /// </summary>
    public DateTime RegistrationTime { get; set; }

    /// <summary>
    /// Last heartbeat time
    /// </summary>
    public DateTime LastHeartbeatTime { get; set; }

    /// <summary>
    /// Service instance metadata
    /// </summary>
    [JsonConverter(typeof(PreserveOriginalConverter<Dictionary<string, string>>))]
    public Dictionary<string, string> Metadata { get; set; } = new();
}
