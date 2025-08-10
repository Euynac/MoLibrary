using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.MoLogProvider;

namespace MoLibrary.Core.Features.MoXmlDocumentation;

/// <summary>
/// XML文档服务实现
/// </summary>
public class XmlDocumentationService : IXmlDocumentationService
{
    private readonly ConcurrentDictionary<string, XmlDocumentCacheInfo> _documentCache = new();
    private readonly ConcurrentDictionary<string, XPathNavigator?> _navigatorCache = new();
    private static readonly Regex CleanWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex RemoveTagsRegex = new(@"<[^>]*>", RegexOptions.Compiled);
    private readonly ILogger<XmlDocumentationService> _logger = LogProvider.For<XmlDocumentationService>();

    /// <summary>
    /// 获取方法的XML文档信息
    /// </summary>
    public XmlMethodDocumentation? GetMethodDocumentation(MethodInfo method)
    {
        try
        {
            var assembly = method.DeclaringType?.Assembly;
            if (assembly == null) return null;

            var navigator = GetOrCreateNavigator(assembly);
            if (navigator == null) return null;

            var memberName = GetMethodMemberName(method);
            var xpath = $"/doc/members/member[@name='{memberName}']";
            var node = navigator.SelectSingleNode(xpath);

            if (node == null) return null;

            var documentation = new XmlMethodDocumentation();

            // 获取summary
            var summaryNode = node.SelectSingleNode("summary");
            if (summaryNode != null)
            {
                documentation.Summary = CleanDocumentation(summaryNode.Value);
            }

            // 获取returns
            var returnsNode = node.SelectSingleNode("returns");
            if (returnsNode != null)
            {
                documentation.Returns = CleanDocumentation(returnsNode.Value);
            }

            // 获取remarks
            var remarksNode = node.SelectSingleNode("remarks");
            if (remarksNode != null)
            {
                documentation.Remarks = CleanDocumentation(remarksNode.Value);
            }

            // 获取参数描述
            var paramNodes = node.Select("param");
            while (paramNodes.MoveNext())
            {
                var paramNode = paramNodes.Current;
                var paramName = paramNode?.GetAttribute("name", "");
                if (!string.IsNullOrEmpty(paramName) && paramNode != null)
                {
                    documentation.Parameters[paramName] = CleanDocumentation(paramNode.Value);
                }
            }

            // 获取异常描述
            var exceptionNodes = node.Select("exception");
            while (exceptionNodes.MoveNext())
            {
                var exceptionNode = exceptionNodes.Current;
                var exceptionType = exceptionNode?.GetAttribute("cref", "");
                if (!string.IsNullOrEmpty(exceptionType) && exceptionNode != null)
                {
                    // 清理exception类型名称 (e.g., "T:System.ArgumentException" -> "ArgumentException")
                    var cleanType = exceptionType.StartsWith("T:") ? exceptionType.Substring(2) : exceptionType;
                    cleanType = cleanType.Contains('.') ? cleanType.Substring(cleanType.LastIndexOf('.') + 1) : cleanType;
                    documentation.Exceptions[cleanType] = CleanDocumentation(exceptionNode.Value);
                }
            }

            return documentation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get method documentation for {Method}", method.Name);
            return null;
        }
    }

    /// <summary>
    /// 获取类型的XML文档信息
    /// </summary>
    public string? GetTypeDocumentation(Type type)
    {
        try
        {
            var assembly = type.Assembly;
            var navigator = GetOrCreateNavigator(assembly);
            if (navigator == null) return null;

            var memberName = GetTypeMemberName(type);
            var xpath = $"/doc/members/member[@name='{memberName}']/summary";
            var node = navigator.SelectSingleNode(xpath);

            return node != null ? CleanDocumentation(node.Value) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get type documentation for {Type}", type.FullName);
            return null;
        }
    }

    /// <summary>
    /// 清空缓存
    /// </summary>
    public void ClearCache()
    {
        _documentCache.Clear();
        _navigatorCache.Clear();
        _logger.LogInformation("XML documentation cache cleared");
    }

    /// <summary>
    /// 获取当前缓存的XML文档信息列表
    /// </summary>
    public IReadOnlyList<XmlDocumentCacheInfo> GetCachedDocuments()
    {
        return _documentCache.Values.ToList();
    }

    /// <summary>
    /// 获取或创建XPath导航器
    /// </summary>
    private XPathNavigator? GetOrCreateNavigator(Assembly assembly)
    {
        var assemblyName = assembly.GetName().Name;
        if (string.IsNullOrEmpty(assemblyName)) return null;

        // 尝试从导航器缓存获取
        if (_navigatorCache.TryGetValue(assemblyName, out var cachedNavigator))
        {
            return cachedNavigator;
        }

        // 尝试加载XML文档
        var xmlPath = GetXmlDocumentationPath(assembly);
        if (string.IsNullOrEmpty(xmlPath))
        {
            _navigatorCache[assemblyName] = null;
            return null;
        }

        if (!File.Exists(xmlPath))
        {
            _logger.LogWarning("Project XML file not found: {FilePath}, you need to add <GenerateDocumentationFile>True</GenerateDocumentationFile> into your .csproj file to generate swagger documents", xmlPath);
            _navigatorCache[assemblyName] = null;
            return null;
        }

        try
        {
            // 创建XPathDocument并缓存
            var document = new XPathDocument(xmlPath);
            var navigator = document.CreateNavigator();

            // 缓存文档信息
            _documentCache[assemblyName] = new XmlDocumentCacheInfo
            {
                AssemblyName = assemblyName,
                XmlFilePath = xmlPath,
                Document = document,
                CachedAt = DateTime.UtcNow
            };

            // 缓存导航器
            _navigatorCache[assemblyName] = navigator;

            _logger.LogDebug("Loaded XML documentation for assembly {Assembly} from {Path}", assemblyName, xmlPath);
            return navigator;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load XML documentation from {Path}", xmlPath);
            _navigatorCache[assemblyName] = null;
            return null;
        }
    }

    /// <summary>
    /// 获取XML文档文件路径
    /// </summary>
    private static string? GetXmlDocumentationPath(Assembly assembly)
    {
        try
        {
            var assemblyLocation = assembly.Location;
            if (string.IsNullOrEmpty(assemblyLocation)) return null;

            var directory = Path.GetDirectoryName(assemblyLocation);
            var fileName = Path.GetFileNameWithoutExtension(assemblyLocation) + ".xml";

            return directory != null ? Path.Combine(directory, fileName) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取方法的成员名称
    /// </summary>
    private static string GetMethodMemberName(MethodInfo method)
    {
        var typeName = GetTypeFullName(method.DeclaringType!);
        var methodName = method.Name;

        // 处理泛型方法
        if (method.IsGenericMethod)
        {
            var genericArgCount = method.GetGenericArguments().Length;
            methodName += $"``{genericArgCount}";
        }

        var parameters = method.GetParameters();
        var parameterNames = parameters.Select(GetParameterTypeName).ToArray();

        var memberName = $"M:{typeName}.{methodName}";

        if (parameterNames.Length > 0)
        {
            memberName += $"({string.Join(",", parameterNames)})";
        }

        return memberName;
    }

    /// <summary>
    /// 获取类型的成员名称
    /// </summary>
    private static string GetTypeMemberName(Type type)
    {
        return $"T:{GetTypeFullName(type)}";
    }

    /// <summary>
    /// 获取参数类型名称
    /// </summary>
    private static string GetParameterTypeName(ParameterInfo parameter)
    {
        var type = parameter.ParameterType;

        // 处理引用类型
        if (type.IsByRef)
        {
            type = type.GetElementType()!;
        }

        // 处理泛型类型
        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            var genericArgs = type.GetGenericArguments();
            var baseName = GetTypeFullName(genericTypeDef);
            
            // 移除泛型参数计数
            var tickIndex = baseName.IndexOf('`');
            if (tickIndex > 0)
            {
                baseName = baseName.Substring(0, tickIndex);
            }

            var genericArgNames = genericArgs.Select(GetTypeFullName);
            return $"{baseName}{{{string.Join(",", genericArgNames)}}}";
        }

        return GetTypeFullName(type);
    }

    /// <summary>
    /// 获取类型的完整名称
    /// </summary>
    private static string GetTypeFullName(Type type)
    {
        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            var dimensions = type.GetArrayRank();
            var suffix = dimensions == 1 ? "[]" : $"[{new string(',', dimensions - 1)}]";
            return GetTypeFullName(elementType) + suffix;
        }

        // 处理嵌套类型
        var fullName = type.FullName?.Replace('+', '.') ?? type.Name;

        // 处理泛型类型参数
        if (type.IsGenericTypeParameter || type.IsGenericMethodParameter)
        {
            return type.IsGenericMethodParameter ? $"``{type.GenericParameterPosition}" : $"`{type.GenericParameterPosition}";
        }

        return fullName;
    }

    /// <summary>
    /// 清理XML文档内容
    /// </summary>
    private static string CleanDocumentation(string xmlContent)
    {
        if (string.IsNullOrEmpty(xmlContent))
            return string.Empty;

        // 移除多余的空白字符和换行符
        var cleaned = CleanWhitespaceRegex.Replace(xmlContent.Trim(), " ");

        // 移除XML标签（如果有的话）
        cleaned = RemoveTagsRegex.Replace(cleaned, "");

        return cleaned.Trim();
    }
}