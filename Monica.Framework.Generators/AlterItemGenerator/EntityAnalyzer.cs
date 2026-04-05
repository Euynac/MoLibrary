using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Monica.Framework.Generators.AlterItemGenerator;

/// <summary>
/// Entity analyzer, used to analyze entity classes and their properties
/// </summary>
internal class EntityAnalyzer(Compilation compilation, CancellationToken cancellationToken)
{
    private readonly Compilation _compilation = compilation;

    /// <summary>
    /// Analyze entity classes and extract all attribute information that needs to be generated
    /// </summary>
    public EntityAnalysisResult? AnalyzeEntity(INamedTypeSymbol entitySymbol)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var properties = new List<PropertyInfo>();
            var ownedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            // Analyze all public properties of an entity
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
    /// Recursively parse properties, handle nested Owned types and optional navigation properties
    /// </summary>
    private void AnalyzePropertiesRecursive(
        INamedTypeSymbol typeSymbol,
        List<PropertyInfo> properties,
        HashSet<INamedTypeSymbol> ownedTypes,
        string propertyPathPrefix,
        INamedTypeSymbol rootEntitySymbol,
        bool isFromOptionalNavigation = false)
    {
        cancellationToken.ThrowIfCancellationRequested();

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

            // Check if it is of type Owned
            if (IsOwnedType(property.Type))
            {
                if (property.Type is INamedTypeSymbol namedType)
                {
                    ownedTypes.Add(namedType);
                    
                    // Recursively analyze properties of Owned type and flatten them
                    AnalyzePropertiesRecursive(namedType, properties, ownedTypes, propertyPath, rootEntitySymbol, isFromOptionalNavigation);
                }
            }
            // Check if optional navigation properties (such as DepInfo, ArrInfo)
            else if (IsOptionalNavigationProperty(property))
            {
                var underlyingType = GetUnderlyingType(property.Type);
                if (underlyingType is INamedTypeSymbol namedType)
                {
                    // Recursively analyze properties of optional navigation properties
                    AnalyzePropertiesRecursive(namedType, properties, ownedTypes, propertyPath, rootEntitySymbol, true);
                }
            }
            else
            {
                // Common properties
                var propertyInfo = CreatePropertyInfo(property, propertyPath, rootEntitySymbol, isFromOptionalNavigation);
                if (propertyInfo != null)
                {
                    properties.Add(propertyInfo);
                }
            }
        }
    }

    /// <summary>
    /// Create attribute information
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
    /// Extract XML document comments
    /// </summary>
    private string? ExtractXmlDocumentation(IPropertySymbol property)
    {
        // Using XML document comments
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
            // Ignore parsing errors
        }

        return null;
    }

    /// <summary>
    /// Check if it is of type Owned
    /// </summary>
    private bool IsOwnedType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        // Check if there is an [Owned] attribute
        return namedType.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name == "OwnedAttribute" || 
                        attr.AttributeClass?.ToDisplayString().Contains("Microsoft.EntityFrameworkCore.OwnedAttribute") == true);
    }

    /// <summary>
    /// Check if optional navigation attribute
    /// </summary>
    private bool IsOptionalNavigationProperty(IPropertySymbol property)
    {
        // If the type is a nullable complex type and is not Owned, it is considered an optional navigation property.
        if (!IsNullableType(property.Type))
            return false;

        var underlyingType = GetUnderlyingType(property.Type);
        return underlyingType is INamedTypeSymbol namedType && 
               !IsBuiltInType(namedType) && 
               !IsOwnedType(namedType);
    }

    /// <summary>
    /// Check if it is a built-in type
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
    /// Check if it is a nullable type
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
    /// Get attribute type string
    /// </summary>
    private string GetPropertyType(ITypeSymbol type)
    {
        // If it is already a nullable type, return directly
        if (IsNullableType(type))
        {
            return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        // For value types, add the nullable modifier
        if (type.IsValueType)
        {
            return $"{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}?";
        }

        // Reference type returns nullable version
        return $"{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}?";
    }

    /// <summary>
    /// Get the underlying type (if it is a nullable type)
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
    /// Check if a property should be ignored
    /// </summary>
    private bool IsIgnoredProperty(IPropertySymbol property)
    {
        // Ignore indexer
        if (property.IsIndexer)
            return true;

        // Check the Ignore setting of the change-item property attribute.
        var alterItemAttr = property.GetAttributes()
            .FirstOrDefault(attr =>
                attr.AttributeClass?.Name == "ChangeItemPropertyAttribute" ||
                attr.AttributeClass?.ToDisplayString().Contains("Monica.Framework.ChangeTracking.Annotations.ChangeItemPropertyAttribute") == true);
        
        if (alterItemAttr != null)
        {
            var ignoreValue = GetAttributeArgumentValue<bool>(alterItemAttr, "Ignore");
            if (ignoreValue)
                return true;
        }

        // Ignore properties with the [NotMapped] attribute
        if (property.GetAttributes().Any(attr => 
            attr.AttributeClass?.Name == "NotMappedAttribute" ||
            attr.AttributeClass?.ToDisplayString().Contains("System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute") == true))
            return true;

        // Ignore properties with [JsonIgnore] attribute
        if (property.GetAttributes().Any(attr => 
            attr.AttributeClass?.Name == "JsonIgnoreAttribute" ||
            attr.AttributeClass?.ToDisplayString().Contains("System.Text.Json.Serialization.JsonIgnoreAttribute") == true))
            return true;

        // Ignore common base class properties
        var ignoredNames = new[] { "Id", "ExtraProperties", "ConcurrencyStamp" };
        if (ignoredNames.Contains(property.Name))
            return true;

        return false;
    }
    
    /// <summary>
    /// Get attribute parameter value
    /// </summary>
    private static T GetAttributeArgumentValue<T>(AttributeData attribute, string parameterName)
    {
        // Find named parameters
        var namedArg = attribute.NamedArguments
            .FirstOrDefault(kvp => kvp.Key == parameterName);

        if (namedArg.Value.IsNull || namedArg.Value.Value == null)
            return default(T)!;

        return (T)namedArg.Value.Value;
    }
}

/// <summary>
/// Entity analysis results
/// </summary>
internal class EntityAnalysisResult(
    INamedTypeSymbol entitySymbol,
    ImmutableList<PropertyInfo> properties,
    ImmutableHashSet<INamedTypeSymbol> ownedTypes)
{
    public INamedTypeSymbol EntitySymbol { get; } = entitySymbol;
    public ImmutableList<PropertyInfo> Properties { get; } = properties;
    public ImmutableHashSet<INamedTypeSymbol> OwnedTypes { get; } = ownedTypes;
}

/// <summary>
/// Attribute information
/// </summary>
internal class PropertyInfo(
    string name,
    string propertyPath,
    string type,
    string originalType,
    bool isNullable,
    bool isOptionalNavigation,
    IPropertySymbol propertySymbol,
    string? xmlDocumentation = null)
{
    public string Name { get; } = name;
    public string PropertyPath { get; } = propertyPath;
    public string Type { get; } = type;
    public string OriginalType { get; } = originalType;
    public bool IsNullable { get; } = isNullable;
    public bool IsOptionalNavigation { get; } = isOptionalNavigation;
    public string? XmlDocumentation { get; } = xmlDocumentation;
    public IPropertySymbol PropertySymbol { get; } = propertySymbol;
}
