namespace Monica.Configuration.Model;

public class ServiceInfo
{
    /// <summary>
    /// Domain name.
    /// </summary>
    public string DomainName { get; set; } = "UnknownDomain";
    /// <summary>
    /// Domain display title.
    /// </summary>
    public string DomainTitle { get; set; } = "UnknownDomain";

    /// <summary>
    /// Microservice project name.
    /// </summary>
    public string ProjectName { get; set; } = "UnknownService";

    /// <summary>
    /// Microservice display name.
    /// </summary>
    public string AppName { get; set; } = "UnknownService";
    /// <summary>
    /// Microservice AppId.
    /// </summary>
    public string AppId { get; set; } = "UnknownService";

}
