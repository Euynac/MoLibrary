using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MoLibrary.Framework.Generators.AlterItemGenerator;

/// <summary>
/// 实体分析器，用于分析实体类及其属性
/// </summary>
internal class EntityAnalyzer
{
    private readonly Compilation _compilation;
    private readonly CancellationToken _cancellationToken;

    public EntityAnalyzer(Compilation compilation, CancellationToken cancellationToken)
    {
        _compilation = compilation;
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    /// 分析实体类，提取所有需要生成的属性信息
    /// </summary>
    public EntityAnalysisResult? AnalyzeEntity(INamedTypeSymbol entitySymbol)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var properties = new List<PropertyInfo>();
            var ownedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            // 分析实体的所有公共属性
            AnalyzePropertiesRecursive(entitySymbol, properties, ownedTypes, "", entitySymbol, false);

            return new EntityAnalysisResult(
                entitySymbol,
                properties.ToImmutableList(),
                ownedTypes.ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default)
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 递归分析属性，处理嵌套的 Owned 类型和可选导航属性
    /// </summary>
    private void AnalyzePropertiesRecursive(
        INamedTypeSymbol typeSymbol,
        List<PropertyInfo> properties,
        HashSet<INamedTypeSymbol> ownedTypes,
        string propertyPathPrefix,
        INamedTypeSymbol rootEntitySymbol,
        bool isFromOptionalNavigation = false)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        var publicProperties = typeSymbol
            .GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public)
            .Where(p => !p.IsStatic)
            .Where(p => p.SetMethod != null) // 必须有 setter
            .Where(p => !IsIgnoredProperty(p))
            .ToList();

        foreach (var property in publicProperties)
        {
            var propertyPath = string.IsNullOrEmpty(propertyPathPrefix) 
                ? property.Name 
                : $"{propertyPathPrefix}.{property.Name}";

            // 检查是否是 Owned 类型
            if (IsOwnedType(property.Type))
            {
                if (property.Type is INamedTypeSymbol namedType)
                {
                    ownedTypes.Add(namedType);
                    
                    // 递归分析 Owned 类型的属性，扁平化处理
                    AnalyzePropertiesRecursive(namedType, properties, ownedTypes, propertyPath, rootEntitySymbol, isFromOptionalNavigation);
                }
            }
            // 检查是否是可选导航属性（如 DepInfo、ArrInfo）
            else if (IsOptionalNavigationProperty(property))
            {
                var underlyingType = GetUnderlyingType(property.Type);
                if (underlyingType is INamedTypeSymbol namedType)
                {
                    // 递归分析可选导航属性的属性
                    AnalyzePropertiesRecursive(namedType, properties, ownedTypes, propertyPath, rootEntitySymbol, true);
                }
            }
            else
            {
                // 普通属性
                var propertyInfo = CreatePropertyInfo(property, propertyPath, rootEntitySymbol, isFromOptionalNavigation);
                if (propertyInfo != null)
                {
                    properties.Add(propertyInfo);
                }
            }
        }
    }

    /// <summary>
    /// 创建属性信息
    /// </summary>
    private PropertyInfo? CreatePropertyInfo(IPropertySymbol property, string propertyPath, INamedTypeSymbol rootEntitySymbol, bool isFromOptionalNavigation = false)
    {
        try
        {
            var xmlDoc = ExtractXmlDocumentation(property);
            var isNullable = IsNullableType(property.Type);
            var propertyType = GetPropertyType(property.Type);
            var isOptionalNavigation = isFromOptionalNavigation || IsOptionalNavigationProperty(property);

            return new PropertyInfo(
                property.Name,
                propertyPath,
                propertyType,
                property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                isNullable,
                isOptionalNavigation,
                property,
                xmlDoc
            );
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 提取 XML 文档注释，优先使用AlterItemPropertyAttribute中的Title
    /// </summary>
    private string? ExtractXmlDocumentation(IPropertySymbol property)
    {
        // 首先检查AlterItemPropertyAttribute中的Title
        var alterItemAttr = property.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name == "AlterItemPropertyAttribute" ||
                                  attr.AttributeClass?.ToDisplayString().Contains("MoLibrary.Framework.Generators.Attributes.AlterItemPropertyAttribute") == true);
        
        if (alterItemAttr != null)
        {
            var title = GetAttributeArgumentValue<string>(alterItemAttr, "Title");
            if (!string.IsNullOrWhiteSpace(title))
                return title;
        }
        
        // 如果没有Title，则使用XML文档注释
        var xmlDoc = property.GetDocumentationCommentXml();
        if (string.IsNullOrWhiteSpace(xmlDoc))
            return null;

        try
        {
            var summaryStart = xmlDoc!.IndexOf("<summary>");
            var summaryEnd = xmlDoc.IndexOf("</summary>");
            
            if (summaryStart >= 0 && summaryEnd > summaryStart)
            {
                var summary = xmlDoc.Substring(summaryStart + 9, summaryEnd - summaryStart - 9).Trim();
                return string.IsNullOrWhiteSpace(summary) ? null : summary;
            }
        }
        catch
        {
            // 忽略解析错误
        }

        return null;
    }

    /// <summary>
    /// 检查是否是 Owned 类型
    /// </summary>
    private bool IsOwnedType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        // 检查是否有 [Owned] 属性
        return namedType.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name == "OwnedAttribute" || 
                        attr.AttributeClass?.ToDisplayString().Contains("Microsoft.EntityFrameworkCore.OwnedAttribute") == true);
    }

    /// <summary>
    /// 检查是否是可选导航属性
    /// </summary>
    private bool IsOptionalNavigationProperty(IPropertySymbol property)
    {
        // 如果类型是可空的复杂类型，且不是 Owned，则认为是可选导航属性
        if (!IsNullableType(property.Type))
            return false;

        var underlyingType = GetUnderlyingType(property.Type);
        return underlyingType is INamedTypeSymbol namedType && 
               !IsBuiltInType(namedType) && 
               !IsOwnedType(namedType);
    }

    /// <summary>
    /// 检查是否是内置类型
    /// </summary>
    private bool IsBuiltInType(INamedTypeSymbol type)
    {
        var typeName = type.ToDisplayString();
        return typeName switch
        {
            "string" or "System.String" => true,
            "int" or "System.Int32" => true,
            "long" or "System.Int64" => true,
            "short" or "System.Int16" => true,
            "byte" or "System.Byte" => true,
            "bool" or "System.Boolean" => true,
            "float" or "System.Single" => true,
            "double" or "System.Double" => true,
            "decimal" or "System.Decimal" => true,
            "System.DateTime" => true,
            "System.DateTimeOffset" => true,
            "System.TimeSpan" => true,
            "System.Guid" => true,
            _ when type.TypeKind == TypeKind.Enum => true,
            _ when type.IsGenericType && type.OriginalDefinition.ToDisplayString().StartsWith("System.Collections.Generic.List<") => true,
            _ when type.IsGenericType && type.OriginalDefinition.ToDisplayString().StartsWith("System.Collections.Generic.IList<") => true,
            _ => false
        };
    }

    /// <summary>
    /// 检查是否是可空类型
    /// </summary>
    private bool IsNullableType(ITypeSymbol type)
    {
        return type.CanBeReferencedByName && 
               (type.IsReferenceType || 
                (type is INamedTypeSymbol namedType && 
                 namedType.IsGenericType && 
                 namedType.OriginalDefinition.ToDisplayString() == "System.Nullable<T>"));
    }

    /// <summary>
    /// 获取属性类型字符串
    /// </summary>
    private string GetPropertyType(ITypeSymbol type)
    {
        // 如果已经是可空类型，直接返回
        if (IsNullableType(type))
        {
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        // 对于值类型，添加可空修饰符
        if (type.IsValueType)
        {
            return $"{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}?";
        }

        // 引用类型返回可空版本
        return $"{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}?";
    }

    /// <summary>
    /// 获取底层类型（如果是可空类型）
    /// </summary>
    private ITypeSymbol GetUnderlyingType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType && 
            namedType.IsGenericType && 
            namedType.OriginalDefinition.ToDisplayString() == "System.Nullable<T>")
        {
            return namedType.TypeArguments[0];
        }

        return type;
    }

    /// <summary>
    /// 检查是否应该忽略的属性
    /// </summary>
    private bool IsIgnoredProperty(IPropertySymbol property)
    {
        // 忽略索引器
        if (property.IsIndexer)
            return true;

        // 检查AlterItemPropertyAttribute的Ignore设置
        var alterItemAttr = property.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name == "AlterItemPropertyAttribute" ||
                                  attr.AttributeClass?.ToDisplayString().Contains("MoLibrary.Framework.Generators.Attributes.AlterItemPropertyAttribute") == true);
        
        if (alterItemAttr != null)
        {
            var ignoreValue = GetAttributeArgumentValue<bool>(alterItemAttr, "Ignore");
            if (ignoreValue)
                return true;
        }

        // 忽略有 [NotMapped] 属性的属性
        if (property.GetAttributes().Any(attr => 
            attr.AttributeClass?.Name == "NotMappedAttribute" ||
            attr.AttributeClass?.ToDisplayString().Contains("System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute") == true))
            return true;

        // 忽略有 [JsonIgnore] 属性的属性
        if (property.GetAttributes().Any(attr => 
            attr.AttributeClass?.Name == "JsonIgnoreAttribute" ||
            attr.AttributeClass?.ToDisplayString().Contains("System.Text.Json.Serialization.JsonIgnoreAttribute") == true))
            return true;

        // 忽略常见的基类属性
        var ignoredNames = new[] { "Id", "ExtraProperties", "ConcurrencyStamp" };
        if (ignoredNames.Contains(property.Name))
            return true;

        return false;
    }
    
    /// <summary>
    /// 获取属性参数值
    /// </summary>
    private static T GetAttributeArgumentValue<T>(AttributeData attribute, string parameterName)
    {
        // 查找命名参数
        var namedArg = attribute.NamedArguments
            .FirstOrDefault(kvp => kvp.Key == parameterName);

        if (namedArg.Value.IsNull || namedArg.Value.Value == null)
            return default(T)!;

        return (T)namedArg.Value.Value;
    }
}

/// <summary>
/// 实体分析结果
/// </summary>
internal class EntityAnalysisResult
{
    public EntityAnalysisResult(INamedTypeSymbol entitySymbol, ImmutableList<PropertyInfo> properties, ImmutableHashSet<INamedTypeSymbol> ownedTypes)
    {
        EntitySymbol = entitySymbol;
        Properties = properties;
        OwnedTypes = ownedTypes;
    }
    
    public INamedTypeSymbol EntitySymbol { get; }
    public ImmutableList<PropertyInfo> Properties { get; }
    public ImmutableHashSet<INamedTypeSymbol> OwnedTypes { get; }
}

/// <summary>
/// 属性信息
/// </summary>
internal class PropertyInfo
{
    public PropertyInfo(string name, string propertyPath, string type, string originalType, bool isNullable, bool isOptionalNavigation, IPropertySymbol propertySymbol, string? xmlDocumentation = null)
    {
        Name = name;
        PropertyPath = propertyPath;
        Type = type;
        OriginalType = originalType;
        IsNullable = isNullable;
        IsOptionalNavigation = isOptionalNavigation;
        PropertySymbol = propertySymbol;
        XmlDocumentation = xmlDocumentation;
    }
    
    public string Name { get; }
    public string PropertyPath { get; }
    public string Type { get; }
    public string OriginalType { get; }
    public bool IsNullable { get; }
    public bool IsOptionalNavigation { get; }
    public string? XmlDocumentation { get; }
    public IPropertySymbol PropertySymbol { get; }
}