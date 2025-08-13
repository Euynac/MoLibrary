namespace MoLibrary.Configuration.Model;

public class ServiceInfo
{
    /// <summary>
    /// 子域名
    /// </summary>
    public string DomainName { get; set; } = "UnknownDomain";
    /// <summary>
    /// 子域标题（显示名）
    /// </summary>
    public string DomainTitle { get; set; } = "UnknownDomain";

    /// <summary>
    /// 微服务项目名
    /// </summary>
    public string ProjectName { get; set; } = "UnknownService";

    /// <summary>
    /// 微服务显示名
    /// </summary>
    public string AppName { get; set; } = "UnknownService";
    /// <summary>
    /// 微服务APPID
    /// </summary>
    public string AppId { get; set; } = "UnknownService";

}