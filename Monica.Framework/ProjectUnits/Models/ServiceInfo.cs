namespace Monica.Framework.ProjectUnits.Models;

public class ServiceInfo
{
    /// <summary>
    /// Subdomain name
    /// </summary>
    public string DomainName { get; set; } = "UnknownDomain";
    /// <summary>
    /// Subdomain title (display name)
    /// </summary>
    public string DomainTitle { get; set; } = "UnknownDomain";

    /// <summary>
    /// Microservice project name
    /// </summary>
    public string ServiceName { get; set; } = "UnknownService";

    /// <summary>
    /// Microservice display name
    /// </summary>
    public string ServiceTitle { get; set; } = "UnknownService";
    /// <summary>
    /// Microservice APPID
    /// </summary>
    public string AppID { get; set; } = "UnknownService";

    /// <summary>
    /// Dependent subdomain list
    /// </summary>
    public List<string> DependencyDomains { get; set; } = [];

    /// <summary>
    /// Service components
    /// </summary>
    public List<ProjectUnit> Units { get; set; } = [];
}