using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using Microsoft.Extensions.Logging;
using Monica.Core.Features.MoLogProvider;

namespace Monica.Core.Features.MoXmlDocumentation;

/// <summary>
/// Default implementation of <see cref="IXmlDocumentationService" />.
/// </summary>
public class XmlDocumentationService : IXmlDocumentationService
{
    private readonly ConcurrentDictionary<string, XmlDocumentCacheInfo> _documentCache = new();
    private readonly ConcurrentDictionary<string, XPathNavigator?> _navigatorCache = new();
    private static readonly Regex CleanWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex RemoveTagsRegex = new(@"<[^>]*>", RegexOptions.Compiled);
    private readonly ILogger<XmlDocumentationService> _logger = LogProvider.For<XmlDocumentationService>();

    /// <summary>
    /// Gets XML documentation for a method.
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

            // Read the summary element.
            var summaryNode = node.SelectSingleNode("summary");
            if (summaryNode != null)
            {
                documentation.Summary = CleanDocumentation(summaryNode.Value);
            }

            // Read the returns element.
            var returnsNode = node.SelectSingleNode("returns");
            if (returnsNode != null)
            {
                documentation.Returns = CleanDocumentation(returnsNode.Value);
            }

            // Read the remarks element.
            var remarksNode = node.SelectSingleNode("remarks");
            if (remarksNode != null)
            {
                documentation.Remarks = CleanDocumentation(remarksNode.Value);
            }

            // Read parameter descriptions.
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

            // Read exception descriptions.
            var exceptionNodes = node.Select("exception");
            while (exceptionNodes.MoveNext())
            {
                var exceptionNode = exceptionNodes.Current;
                var exceptionType = exceptionNode?.GetAttribute("cref", "");
                if (!string.IsNullOrEmpty(exceptionType) && exceptionNode != null)
                {
                    // Normalize the cref value (for example "T:System.ArgumentException" => "ArgumentException").
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
    /// Gets the summary documentation for a type.
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
    /// Clears the cached XML documents.
    /// </summary>
    public void ClearCache()
    {
        _documentCache.Clear();
        _navigatorCache.Clear();
        _logger.LogInformation("XML documentation cache cleared");
    }

    /// <summary>
    /// Gets information about the currently cached XML documents.
    /// </summary>
    public IReadOnlyList<XmlDocumentCacheInfo> GetCachedDocuments()
    {
        return _documentCache.Values.ToList();
    }

    /// <summary>
    /// Gets a cached navigator for the assembly or creates one from its XML file.
    /// </summary>
    private XPathNavigator? GetOrCreateNavigator(Assembly assembly)
    {
        var assemblyName = assembly.GetName().Name;
        if (string.IsNullOrEmpty(assemblyName)) return null;

        // Reuse the cached navigator when available.
        if (_navigatorCache.TryGetValue(assemblyName, out var cachedNavigator))
        {
            return cachedNavigator;
        }

        // Locate and load the XML documentation file.
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
            // Create and cache the XPath document.
            var document = new XPathDocument(xmlPath);
            var navigator = document.CreateNavigator();

            // Cache the document metadata.
            _documentCache[assemblyName] = new XmlDocumentCacheInfo
            {
                AssemblyName = assemblyName,
                XmlFilePath = xmlPath,
                Document = document,
                CachedAt = DateTime.UtcNow
            };

            // Cache the navigator for reuse.
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
    /// Gets the XML documentation path for an assembly.
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
    /// Builds the XML documentation member name for a method.
    /// </summary>
    private static string GetMethodMemberName(MethodInfo method)
    {
        var typeName = GetTypeFullName(method.DeclaringType!);
        var methodName = method.Name;

        // Encode generic method arity using the XML documentation naming rules.
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
    /// Builds the XML documentation member name for a type.
    /// </summary>
    private static string GetTypeMemberName(Type type)
    {
        return $"T:{GetTypeFullName(type)}";
    }

    /// <summary>
    /// Builds the XML documentation type name for a method parameter.
    /// </summary>
    private static string GetParameterTypeName(ParameterInfo parameter)
    {
        var type = parameter.ParameterType;

        // Use the element type for ref and out parameters.
        if (type.IsByRef)
        {
            type = type.GetElementType()!;
        }

        // Format generic types using XML documentation conventions.
        if (type.IsGenericType)
        {
            var genericTypeDef = type.GetGenericTypeDefinition();
            var genericArgs = type.GetGenericArguments();
            var baseName = GetTypeFullName(genericTypeDef);
            
            // Remove the generic arity suffix from the base type name.
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
    /// Gets the full XML documentation type name.
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

        // XML documentation uses dots for nested types.
        var fullName = type.FullName?.Replace('+', '.') ?? type.Name;

        // Generic type parameters use backtick-based positional notation.
        if (type.IsGenericTypeParameter || type.IsGenericMethodParameter)
        {
            return type.IsGenericMethodParameter ? $"``{type.GenericParameterPosition}" : $"`{type.GenericParameterPosition}";
        }

        return fullName;
    }

    /// <summary>
    /// Normalizes raw XML documentation text.
    /// </summary>
    private static string CleanDocumentation(string xmlContent)
    {
        if (string.IsNullOrEmpty(xmlContent))
            return string.Empty;

        // Collapse repeated whitespace and line breaks.
        var cleaned = CleanWhitespaceRegex.Replace(xmlContent.Trim(), " ");

        // Remove any remaining XML tags.
        cleaned = RemoveTagsRegex.Replace(cleaned, "");

        return cleaned.Trim();
    }
}
