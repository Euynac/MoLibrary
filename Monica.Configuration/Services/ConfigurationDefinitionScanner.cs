using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Annotations;
using Monica.Configuration.Models;
using Monica.Configuration.Services.Support;

namespace Monica.Configuration.Services;

/// <summary>
/// Reflection scanner that converts CLR options types into schema definitions.
/// </summary>
internal sealed class ConfigurationDefinitionScanner(ConfigurationSchemaHasher hasher) : IConfigurationDefinitionScanner
{
    /// <inheritdoc />
    public ConfigurationDefinition Scan(Type optionsType)
    {
        var attribute = optionsType.GetCustomAttribute<ConfigurationAttribute>()
            ?? throw new InvalidOperationException($"Type '{optionsType.FullName}' is not marked with {nameof(ConfigurationAttribute)}.");

        var definitionKey = attribute.DefinitionKey ?? optionsType.FullName ?? optionsType.Name;
        var sectionPath = attribute.SectionPath ?? definitionKey.Replace('.', ':');
        var root = ScanNode(optionsType, optionsType.Name, LogicalPath.Root, sectionPath, attribute.ReloadBehavior);

        return new ConfigurationDefinition
        {
            DefinitionKey = definitionKey,
            SectionPath = sectionPath,
            DisplayName = attribute.DisplayName ?? optionsType.Name,
            ClrTypeName = optionsType.AssemblyQualifiedName ?? optionsType.FullName ?? optionsType.Name,
            OwnerModule = attribute.OwnerModule,
            Category = attribute.Category,
            ReloadBehavior = attribute.ReloadBehavior,
            Root = root,
            SchemaHash = hasher.ComputeHash(definitionKey, sectionPath, root)
        };
    }

    private static ConfigurationNodeDefinition ScanNode(
        Type type,
        string name,
        LogicalPath path,
        string configurationPath,
        ConfigurationReloadBehavior inheritedReloadBehavior)
    {
        var option = type.GetCustomAttribute<OptionSettingAttribute>();
        var nodeKind = GetNodeKind(type);
        var children = nodeKind == ConfigurationNodeKind.Object
            ? ScanObjectChildren(type, path, configurationPath, inheritedReloadBehavior)
            : [];

        return new ConfigurationNodeDefinition
        {
            NodeKey = option?.NodeKey ?? path.ToCanonicalString(),
            Name = name,
            DisplayName = option?.DisplayName,
            Description = option?.Description,
            RelativePath = path,
            ConfigurationPath = configurationPath,
            ClrTypeName = type.AssemblyQualifiedName ?? type.FullName ?? type.Name,
            NodeKind = nodeKind,
            ValueKind = nodeKind == ConfigurationNodeKind.Scalar ? GetValueKind(type) : null,
            IsNullable = IsNullable(type),
            IsSensitive = option?.IsSensitive is true,
            ReloadBehavior = ResolveReloadBehavior(option),
            DictionaryTemplate = nodeKind == ConfigurationNodeKind.Dictionary
                ? BuildDictionaryTemplate(type, path, configurationPath, inheritedReloadBehavior)
                : null,
            ListTemplate = nodeKind == ConfigurationNodeKind.List
                ? BuildListTemplate(type, path, configurationPath, inheritedReloadBehavior)
                : null,
            Children = children,
            ValidationRules = GetValidationRules(type.GetCustomAttributes<ValidationAttribute>())
        };
    }

    private static IReadOnlyList<ConfigurationNodeDefinition> ScanObjectChildren(
        Type type,
        LogicalPath parentPath,
        string parentConfigurationPath,
        ConfigurationReloadBehavior inheritedReloadBehavior)
    {
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetMethod is not null && property.GetIndexParameters().Length == 0)
            .Select(property =>
            {
                var propertyName = GetConfigurationPropertyName(property);
                var childPath = parentPath.Append(new PropertySegment(propertyName));
                var childConfigurationPath = string.IsNullOrWhiteSpace(parentConfigurationPath)
                    ? propertyName
                    : $"{parentConfigurationPath}:{propertyName}";
                var option = property.GetCustomAttribute<OptionSettingAttribute>();
                var propertyType = property.PropertyType;
                return ScanPropertyNode(property, option, propertyType, propertyName, childPath, childConfigurationPath, inheritedReloadBehavior);
            })
            .ToArray();
    }

    private static ConfigurationNodeDefinition ScanPropertyNode(
        PropertyInfo property,
        OptionSettingAttribute? option,
        Type propertyType,
        string nodeName,
        LogicalPath path,
        string configurationPath,
        ConfigurationReloadBehavior inheritedReloadBehavior)
    {
        var nodeKind = GetNodeKind(propertyType);
        var children = nodeKind == ConfigurationNodeKind.Object
            ? ScanObjectChildren(propertyType, path, configurationPath, inheritedReloadBehavior)
            : [];

        return new ConfigurationNodeDefinition
        {
            NodeKey = option?.NodeKey ?? path.ToCanonicalString(),
            Name = nodeName,
            DisplayName = option?.DisplayName,
            Description = option?.Description,
            RelativePath = path,
            ConfigurationPath = configurationPath,
            ClrTypeName = propertyType.AssemblyQualifiedName ?? propertyType.FullName ?? propertyType.Name,
            NodeKind = nodeKind,
            ValueKind = nodeKind == ConfigurationNodeKind.Scalar ? GetValueKind(propertyType) : null,
            IsNullable = IsNullable(property),
            IsSensitive = option?.IsSensitive is true,
            ReloadBehavior = ResolveReloadBehavior(option),
            DictionaryTemplate = nodeKind == ConfigurationNodeKind.Dictionary
                ? BuildDictionaryTemplate(propertyType, path, configurationPath, inheritedReloadBehavior)
                : null,
            ListTemplate = nodeKind == ConfigurationNodeKind.List
                ? BuildListTemplate(propertyType, path, configurationPath, inheritedReloadBehavior)
                : null,
            Children = children,
            ValidationRules = GetValidationRules(property.GetCustomAttributes<ValidationAttribute>())
        };
    }

    private static ConfigurationDictionaryTemplate BuildDictionaryTemplate(
        Type dictionaryType,
        LogicalPath dictionaryPath,
        string configurationPath,
        ConfigurationReloadBehavior inheritedReloadBehavior)
    {
        var keyType = typeof(string);
        var valueType = typeof(object);
        var dictionaryInterface = GetClosedGenericInterface(dictionaryType, typeof(IDictionary<,>));
        if (dictionaryInterface is not null)
        {
            var arguments = dictionaryInterface.GetGenericArguments();
            keyType = arguments[0];
            valueType = arguments[1];
        }

        return new ConfigurationDictionaryTemplate
        {
            KeyClrTypeName = keyType.AssemblyQualifiedName ?? keyType.FullName ?? keyType.Name,
            KeyKind = GetValueKind(keyType),
            ValueTemplate = ScanNode(
                valueType,
                "Value",
                dictionaryPath.Append(new DictionaryKeySegment("*")),
                string.IsNullOrWhiteSpace(configurationPath) ? "*" : $"{configurationPath}:*",
                inheritedReloadBehavior)
        };
    }

    private static ConfigurationListTemplate BuildListTemplate(
        Type listType,
        LogicalPath listPath,
        string configurationPath,
        ConfigurationReloadBehavior inheritedReloadBehavior)
    {
        var itemType = GetEnumerableItemType(listType) ?? typeof(object);
        return new ConfigurationListTemplate
        {
            ItemKeyPropertyName = ResolveListItemKeyPropertyName(itemType),
            ItemTemplate = ScanNode(
                itemType,
                "Item",
                listPath.Append(new ListItemKeySegment("*")),
                string.IsNullOrWhiteSpace(configurationPath) ? "*" : $"{configurationPath}:*",
                inheritedReloadBehavior)
        };
    }

    private static string? ResolveListItemKeyPropertyName(Type itemType)
    {
        var keyProperties = itemType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.GetCustomAttribute<OptionSettingAttribute>()?.IsListItemKey is true)
            .ToArray();

        if (keyProperties.Length == 0)
        {
            return null;
        }

        if (keyProperties.Length > 1)
        {
            var propertyNames = string.Join(", ", keyProperties.Select(property => property.Name));
            throw new InvalidOperationException(
                $"List item type '{itemType.FullName}' declares multiple list item key properties: {propertyNames}.");
        }

        var keyProperty = keyProperties[0];
        if (keyProperty.GetMethod is null
            || !keyProperty.GetMethod.IsPublic
            || keyProperty.GetIndexParameters().Length > 0
            || GetNodeKind(keyProperty.PropertyType) != ConfigurationNodeKind.Scalar)
        {
            throw new InvalidOperationException(
                $"List item key property '{itemType.FullName}.{keyProperty.Name}' must be a public scalar property.");
        }

        return GetConfigurationPropertyName(keyProperty);
    }

    private static ConfigurationNodeKind GetNodeKind(Type type)
    {
        var actual = Nullable.GetUnderlyingType(type) ?? type;
        if (IsScalar(actual))
        {
            return ConfigurationNodeKind.Scalar;
        }

        if (actual != typeof(string) && typeof(IDictionary).IsAssignableFrom(actual))
        {
            return ConfigurationNodeKind.Dictionary;
        }

        if (actual != typeof(string) && typeof(IEnumerable).IsAssignableFrom(actual))
        {
            return ConfigurationNodeKind.List;
        }

        return ConfigurationNodeKind.Object;
    }

    private static Type? GetClosedGenericInterface(Type type, Type genericInterfaceDefinition)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == genericInterfaceDefinition)
        {
            return type;
        }

        return type.GetInterfaces()
            .FirstOrDefault(candidate => candidate.IsGenericType
                                         && candidate.GetGenericTypeDefinition() == genericInterfaceDefinition);
    }

    private static Type? GetEnumerableItemType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        var enumerableInterface = GetClosedGenericInterface(type, typeof(IEnumerable<>));
        return enumerableInterface?.GetGenericArguments()[0];
    }

    private static bool IsScalar(Type type)
    {
        return type.IsPrimitive
               || type.IsEnum
               || type == typeof(string)
               || type == typeof(decimal)
               || type == typeof(DateTime)
               || type == typeof(DateTimeOffset)
               || type == typeof(TimeSpan)
               || type == typeof(Uri)
               || type == typeof(Guid);
    }

    private static ConfigurationValueKind GetValueKind(Type type)
    {
        var actual = Nullable.GetUnderlyingType(type) ?? type;
        if (actual == typeof(bool))
        {
            return ConfigurationValueKind.Boolean;
        }

        if (actual.IsEnum)
        {
            return ConfigurationValueKind.Enum;
        }

        if (actual == typeof(decimal))
        {
            return ConfigurationValueKind.Decimal;
        }

        if (actual == typeof(float) || actual == typeof(double))
        {
            return ConfigurationValueKind.Floating;
        }

        if (actual == typeof(DateTime) || actual == typeof(DateTimeOffset))
        {
            return ConfigurationValueKind.DateTime;
        }

        if (actual == typeof(TimeSpan))
        {
            return ConfigurationValueKind.TimeSpan;
        }

        if (actual == typeof(Uri))
        {
            return ConfigurationValueKind.Uri;
        }

        if (!IsScalar(actual))
        {
            return ConfigurationValueKind.Json;
        }

        if (actual == typeof(byte)
            || actual == typeof(short)
            || actual == typeof(int)
            || actual == typeof(long)
            || actual == typeof(sbyte)
            || actual == typeof(ushort)
            || actual == typeof(uint)
            || actual == typeof(ulong))
        {
            return ConfigurationValueKind.Integer;
        }

        return ConfigurationValueKind.String;
    }

    private static string GetConfigurationPropertyName(PropertyInfo property)
    {
        return property.GetCustomAttribute<ConfigurationKeyNameAttribute>()?.Name ?? property.Name;
    }

    private static bool IsNullable(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
    }

    private static bool IsNullable(PropertyInfo property)
    {
        return IsNullable(property.PropertyType);
    }

    private static IReadOnlyList<ConfigurationValidationRule> GetValidationRules(IEnumerable<ValidationAttribute> attributes)
    {
        return attributes.Select<ValidationAttribute, ConfigurationValidationRule?>(attribute => attribute switch
            {
                RequiredAttribute required => new RequiredRule { ErrorMessage = required.ErrorMessage },
                RangeAttribute range => new RangeRule(ToDecimal(range.Minimum), ToDecimal(range.Maximum)) { ErrorMessage = range.ErrorMessage },
                RegularExpressionAttribute regex => new RegexRule(regex.Pattern) { ErrorMessage = regex.ErrorMessage },
                MaxLengthAttribute max => new MaxLengthRule(max.Length) { ErrorMessage = max.ErrorMessage },
                MinLengthAttribute min => new MinLengthRule(min.Length) { ErrorMessage = min.ErrorMessage },
                StringLengthAttribute length => new MaxLengthRule(length.MaximumLength) { ErrorMessage = length.ErrorMessage },
                _ => null
            })
            .Where(rule => rule is not null)
            .Cast<ConfigurationValidationRule>()
            .ToArray();
    }

    private static decimal? ToDecimal(object? value)
    {
        return value is null ? null : Convert.ToDecimal(value);
    }

    private static ConfigurationReloadBehavior? ResolveReloadBehavior(OptionSettingAttribute? option)
    {
        return option is null || option.ReloadBehavior == ConfigurationReloadBehavior.Inherit
            ? null
            : option.ReloadBehavior;
    }
}
