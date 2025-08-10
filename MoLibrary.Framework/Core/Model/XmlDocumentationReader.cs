using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MoLibrary.Framework.Core.Model;

/// <summary>
/// XML文档注释读取器
/// </summary>
public static class XmlDocumentationReader
{
    private static readonly Dictionary<Assembly, XDocument?> _xmlDocCache = new();
    private static readonly object _lock = new();

    /// <summary>
    /// 获取方法的XML注释描述
    /// </summary>
    /// <param name="method">方法信息</param>
    /// <returns>方法描述，如果没有则返回null</returns>
    public static string? GetMethodDescription(MethodInfo method)
    {
        try
        {
            var assembly = method.DeclaringType?.Assembly;
            if (assembly == null) return null;

            var xmlDoc = GetXmlDocument(assembly);
            if (xmlDoc == null) return null;

            var memberName = GetMemberName(method);
            var memberElement = xmlDoc.Descendants("member")
                .FirstOrDefault(x => x.Attribute("name")?.Value == memberName);

            var summaryElement = memberElement?.Element("summary");
            if (summaryElement == null) return null;

            // 清理XML注释中的空白字符和换行符
            return CleanXmlDocumentation(summaryElement.Value);
        }
        catch
        {
            // 如果解析失败，返回null
            return null;
        }
    }

    /// <summary>
    /// 获取程序集对应的XML文档
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <returns>XML文档，如果不存在则返回null</returns>
    private static XDocument? GetXmlDocument(Assembly assembly)
    {
        lock (_lock)
        {
            if (_xmlDocCache.TryGetValue(assembly, out var cachedDoc))
                return cachedDoc;

            try
            {
                var xmlPath = GetXmlDocumentationPath(assembly);
                if (xmlPath == null || !File.Exists(xmlPath))
                {
                    _xmlDocCache[assembly] = null;
                    return null;
                }

                var xmlDoc = XDocument.Load(xmlPath);
                _xmlDocCache[assembly] = xmlDoc;
                return xmlDoc;
            }
            catch
            {
                _xmlDocCache[assembly] = null;
                return null;
            }
        }
    }

    /// <summary>
    /// 获取XML文档文件路径
    /// </summary>
    /// <param name="assembly">程序集</param>
    /// <returns>XML文档文件路径，如果不存在则返回null</returns>
    private static string? GetXmlDocumentationPath(Assembly assembly)
    {
        try
        {
            var assemblyLocation = assembly.Location;
            if (string.IsNullOrEmpty(assemblyLocation)) return null;

            var directory = Path.GetDirectoryName(assemblyLocation);
            var fileName = Path.GetFileNameWithoutExtension(assemblyLocation) + ".xml";
            
            if (directory == null) return null;
            
            var xmlPath = Path.Combine(directory, fileName);
            return xmlPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取方法的成员名称（用于XML文档查找）
    /// </summary>
    /// <param name="method">方法信息</param>
    /// <returns>成员名称</returns>
    private static string GetMemberName(MethodInfo method)
    {
        var typeName = method.DeclaringType?.FullName?.Replace('+', '.');
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
    /// 获取参数类型名称
    /// </summary>
    /// <param name="parameter">参数信息</param>
    /// <returns>类型名称</returns>
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
            var genericArgNames = genericArgs.Select(t => GetTypeFullName(t));
            return $"{GetTypeFullName(genericTypeDef).Replace($"`{genericArgs.Length}", "")}{{{string.Join(",", genericArgNames)}}}";
        }
        
        return GetTypeFullName(type);
    }

    /// <summary>
    /// 获取类型的完整名称
    /// </summary>
    /// <param name="type">类型</param>
    /// <returns>完整名称</returns>
    private static string GetTypeFullName(Type type)
    {
        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            var dimensions = type.GetArrayRank();
            var suffix = dimensions == 1 ? "[]" : $"[{new string(',', dimensions - 1)}]";
            return GetTypeFullName(elementType) + suffix;
        }
        
        return type.FullName?.Replace('+', '.') ?? type.Name;
    }

    /// <summary>
    /// 清理XML文档注释内容
    /// </summary>
    /// <param name="xmlContent">原始XML内容</param>
    /// <returns>清理后的内容</returns>
    private static string CleanXmlDocumentation(string xmlContent)
    {
        if (string.IsNullOrEmpty(xmlContent))
            return string.Empty;

        // 移除多余的空白字符和换行符
        var cleaned = Regex.Replace(xmlContent.Trim(), @"\s+", " ");
        
        // 移除XML标签（如果有的话）
        cleaned = Regex.Replace(cleaned, @"<[^>]*>", "");
        
        return cleaned.Trim();
    }
}