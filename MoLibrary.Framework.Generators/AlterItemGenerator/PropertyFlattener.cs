using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace MoLibrary.Framework.Generators.AlterItemGenerator;

/// <summary>
/// 属性扁平化器，将嵌套对象的属性展平为单层结构
/// </summary>
internal class PropertyFlattener
{
    /// <summary>
    /// 扁平化属性列表
    /// </summary>
    public FlattenedPropertyResult FlattenProperties(EntityAnalysisResult analysisResult)
    {
        var flattenedProperties = new List<FlattenedProperty>();
        var navigationGroups = new Dictionary<string, NavigationPropertyGroup>();

        foreach (var property in analysisResult.Properties)
        {
            if (property.IsOptionalNavigation)
            {
                // 可选导航属性需要分组处理
                ProcessOptionalNavigationProperty(property, navigationGroups, analysisResult.EntitySymbol);
            }
            else
            {
                // 普通属性或从 Owned 类型扁平化的属性
                var flattenedProperty = CreateFlattenedProperty(property);
                flattenedProperties.Add(flattenedProperty);
            }
        }

        return new FlattenedPropertyResult(
            flattenedProperties.ToImmutableList(),
            navigationGroups.Values.ToImmutableList()
        );
    }

    /// <summary>
    /// 处理可选导航属性
    /// </summary>
    private void ProcessOptionalNavigationProperty(PropertyInfo property, Dictionary<string, NavigationPropertyGroup> navigationGroups, Microsoft.CodeAnalysis.INamedTypeSymbol entitySymbol)
    {
        // 从属性路径中提取导航属性名称
        var pathParts = property.PropertyPath.Split('.');
        var navigationPropertyName = pathParts[0];

        if (!navigationGroups.TryGetValue(navigationPropertyName, out var group))
        {
            group = new NavigationPropertyGroup(
                navigationPropertyName,
                new List<FlattenedProperty>(),
                entitySymbol // 传入根实体符号用于类型解析
            );
            navigationGroups[navigationPropertyName] = group;
        }

        var flattenedProperty = CreateFlattenedProperty(property);
        group.Properties.Add(flattenedProperty);
    }

    /// <summary>
    /// 创建扁平化属性
    /// </summary>
    private FlattenedProperty CreateFlattenedProperty(PropertyInfo property)
    {
        return new FlattenedProperty(
            GetFlattenedPropertyName(property),
            property.Type,
            property.OriginalType,
            property.PropertyPath,
            property.PropertyPath.Contains('.') && !property.IsOptionalNavigation,
            property.IsOptionalNavigation,
            property.PropertySymbol,
            property.XmlDocumentation
        );
    }

    /// <summary>
    /// 获取扁平化后的属性名称
    /// </summary>
    private string GetFlattenedPropertyName(PropertyInfo property)
    {
        // 对于嵌套属性，使用最后一段作为属性名
        // 例如: Plan.Callsign -> Callsign
        //      DepInfo.COBT -> COBT
        var pathParts = property.PropertyPath.Split('.');
        return pathParts[pathParts.Length - 1];
    }
}

/// <summary>
/// 扁平化属性结果
/// </summary>
internal class FlattenedPropertyResult(
    ImmutableList<FlattenedProperty> properties,
    ImmutableList<NavigationPropertyGroup> navigationGroups)
{
    public ImmutableList<FlattenedProperty> Properties { get; } = properties;
    public ImmutableList<NavigationPropertyGroup> NavigationGroups { get; } = navigationGroups;
}

/// <summary>
/// 扁平化的属性
/// </summary>
internal class FlattenedProperty(
    string name,
    string type,
    string originalType,
    string propertyPath,
    bool isFromOwnedType,
    bool isOptionalNavigation,
    Microsoft.CodeAnalysis.IPropertySymbol originalPropertySymbol,
    string? xmlDocumentation = null)
{
    public string Name { get; } = name;
    public string Type { get; } = type;
    public string OriginalType { get; } = originalType;
    public string PropertyPath { get; } = propertyPath;
    public string? XmlDocumentation { get; } = xmlDocumentation;
    public bool IsFromOwnedType { get; } = isFromOwnedType;
    public bool IsOptionalNavigation { get; } = isOptionalNavigation;
    public Microsoft.CodeAnalysis.IPropertySymbol OriginalPropertySymbol { get; } = originalPropertySymbol;
}

/// <summary>
/// 导航属性组
/// </summary>
internal class NavigationPropertyGroup
{
    public NavigationPropertyGroup(string navigationPropertyName, List<FlattenedProperty> properties, Microsoft.CodeAnalysis.INamedTypeSymbol? entitySymbol = null)
    {
        NavigationPropertyName = navigationPropertyName;
        Properties = properties;
        
        // 直接从根实体类型中查找导航属性
        var navProperty = entitySymbol?.GetMembers().OfType<Microsoft.CodeAnalysis.IPropertySymbol>()
            .FirstOrDefault(p => p.Name == navigationPropertyName);
                
        if (navProperty != null)
        {
            NavigationPropertyTypeName = GetNavigationPropertyTypeName(navProperty.Type);
        }

        NavigationPropertyTypeName ??= $"Unknown{navigationPropertyName}";
    }
    
    public string NavigationPropertyName { get; }
    public List<FlattenedProperty> Properties { get; }
    public string NavigationPropertyTypeName { get; private set; }
    
    private string GetNavigationPropertyTypeName(Microsoft.CodeAnalysis.ITypeSymbol type)
    {
        // 如果是可空类型，获取底层类型
        if (type is Microsoft.CodeAnalysis.INamedTypeSymbol namedType && 
            namedType.IsGenericType && 
            namedType.OriginalDefinition.ToDisplayString() == "System.Nullable<T>")
        {
            return namedType.TypeArguments[0].Name;
        }
        
        return type.Name;
    }
}