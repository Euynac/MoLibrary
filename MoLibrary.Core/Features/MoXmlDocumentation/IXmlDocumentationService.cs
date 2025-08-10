using System.Reflection;
using System.Xml.XPath;

namespace MoLibrary.Core.Features.MoXmlDocumentation;

/// <summary>
/// XML文档服务接口
/// </summary>
public interface IXmlDocumentationService
{
    /// <summary>
    /// 获取方法的XML文档信息
    /// </summary>
    /// <param name="method">方法信息</param>
    /// <returns>XML文档信息</returns>
    XmlMethodDocumentation? GetMethodDocumentation(MethodInfo method);

    /// <summary>
    /// 获取类型的XML文档信息
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>XML文档描述</returns>
    string? GetTypeDocumentation(Type type);

    /// <summary>
    /// 清空缓存，释放内存
    /// </summary>
    void ClearCache();

    /// <summary>
    /// 获取当前缓存的XML文档信息列表
    /// </summary>
    /// <returns>缓存的XML文档信息</returns>
    IReadOnlyList<XmlDocumentCacheInfo> GetCachedDocuments();
}

/// <summary>
/// XML方法文档信息
/// </summary>
public class XmlMethodDocumentation
{
    /// <summary>
    /// 方法摘要
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// 返回值描述
    /// </summary>
    public string? Returns { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remarks { get; set; }

    /// <summary>
    /// 参数描述字典
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = new();

    /// <summary>
    /// 异常描述字典
    /// </summary>
    public Dictionary<string, string> Exceptions { get; set; } = new();
}

/// <summary>
/// XML文档缓存信息
/// </summary>
public class XmlDocumentCacheInfo
{
    /// <summary>
    /// 程序集名称
    /// </summary>
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>
    /// XML文件路径
    /// </summary>
    public string XmlFilePath { get; set; } = string.Empty;

    /// <summary>
    /// XPath文档对象
    /// </summary>
    public XPathDocument? Document { get; set; }

    /// <summary>
    /// 缓存时间
    /// </summary>
    public DateTime CachedAt { get; set; }
}