namespace MoLibrary.Framework.Core.Model;

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
    public string ServiceName { get; set; } = "UnknownService";

    /// <summary>
    /// 微服务显示名
    /// </summary>
    public string ServiceTitle { get; set; } = "UnknownService";
    /// <summary>
    /// 微服务APPID
    /// </summary>
    public string AppID { get; set; } = "UnknownService";

    /// <summary>
    /// 依赖子域名列表
    /// </summary>
    public List<string> DependencyDomains { get; set; } = [];

    /// <summary>
    /// 服务组成要素
    /// </summary>
    public List<ProjectUnit> Units { get; set; } = [];
}