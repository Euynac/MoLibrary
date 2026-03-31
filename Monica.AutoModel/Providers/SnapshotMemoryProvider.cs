using System.Collections;
using System.Collections.Frozen;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Monica.AutoModel.Abstractions;
using Monica.AutoModel.Annotations;
using Monica.AutoModel.Exceptions;
using Monica.AutoModel.Models;
using Monica.Modules;
using Monica.Tool.Extensions;

// ReSharper disable StaticMemberInGenericType

namespace Monica.AutoModel.Providers;

/// <summary>
/// Builds and caches in-memory AutoModel snapshots.
/// </summary>
/// <typeparam name="TModel">The model type.</typeparam>
public class SnapshotMemoryProvider<TModel> : IAutoModelSnapshot<TModel>
{
    private static AutoModelSnapshot _snapshot = null!;
    private static FrozenDictionary<string, AutoField>? _fieldDictionary;
    private static IReadOnlyList<string> _allActivateNames = null!;

    public SnapshotMemoryProvider(IOptions<ModuleAutoModelOption> options)
    {
        Init(options.Value);
    }

    private static IEnumerable<PropertyInfo> GetAutoFieldTypes(Type type)
    {
        // Note: string is also a reference type. string? only adds NullableContextAttribute, so treat string? the same as string by checking typeof(string).
        return type.GetProperties().OrderByDescending(p => p.PropertyType == typeof(string)).ThenBy(p => p.PropertyType.IsClass);
    }

    private static void Init(ModuleAutoModelOption options)
    {
        Dictionary<string, AutoField> fieldDictionary = [];
        var table = new AutoTable()
        {
            FullTypeName = typeof(TModel).FullName ?? throw new InvalidOperationException(),
            Name = typeof(TModel).Name
        };
        _snapshot = new AutoModelSnapshot
        {
            Table = table,
            Fields = []
        };
        var isActiveMode = options.EnableActiveMode;
        var tableAttribute = typeof(TModel).GetCustomAttribute<AutoTableAttribute>();
        if (tableAttribute != null)
        {
            _snapshot.Table.Name = tableAttribute.Name;
            isActiveMode = tableAttribute.ActiveMode ?? isActiveMode;
        }

        var navigatedTypeNames = new HashSet<Type>(); // Prevent recursive navigation from causing stack overflows. Only one nested level is supported for the same type.
        List<string> allOriginActivateNames = [];

        ExtractFieldInfo(GetAutoFieldTypes(typeof(TModel)));
        _fieldDictionary = fieldDictionary.ToFrozenDictionary();
        SnapshotFactoryMemoryProvider.Snapshots.Add(_snapshot);
        return;

        void ExtractFieldInfo(IEnumerable<PropertyInfo> propertyInfos, PropertyInfo? fromNavigateProperty = null, List<(string, bool)>? previousNavigateTuples = null)
        {

            if (fromNavigateProperty?.Name is { } navigationPropertyName)
            {
                previousNavigateTuples ??= [];
                var isICollection = fromNavigateProperty?.PropertyType.IsAssignableTo(typeof(IEnumerable)) ?? false;
                previousNavigateTuples.Add((navigationPropertyName, isICollection));
            }


            foreach (var p in propertyInfos)
            {
                var fieldAttribute = p.GetCustomAttribute<AutoFieldAttribute>();
                // Skip when active mode is enabled but AutoFieldAttribute is missing.
                if (isActiveMode && fieldAttribute == null) continue;
                if (fieldAttribute?.Ignore is true) continue;
                // Automatically ignore fields without AutoField when they are annotated with NotMapped or JsonIgnore.
                if (fieldAttribute is null)
                {
                    if (!options.DisableAutoIgnorePropertyWithNotMappedAttribute && p.GetCustomAttribute<NotMappedAttribute>() != null) continue;
                    if (!options.DisableAutoIgnorePropertyWithJsonIgnoreAttribute && p.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;
                }


                // Dictionary types are not supported.
                if (typeof(IDictionary).IsAssignableFrom(p.PropertyType)) continue;

                // Determine whether the property should be treated as a navigation property.
                if (p.PropertyType.GetGenericUnderlyingType() is { IsClass: true } underlyingType && underlyingType != typeof(string))
                {
                    if (!navigatedTypeNames.Add(underlyingType)) continue;
                    ExtractFieldInfo(GetAutoFieldTypes(underlyingType), p, previousNavigateTuples?.ToList());
                    continue;
                }

                var activateNames = new List<string>();
                try
                {
                    var field = new AutoField
                    {
                        EnableIgnorePrefix = fieldAttribute?.EnableIgnorePrefix ?? tableAttribute?.EnableIgnorePrefix ?? options.EnableIgnorePrefix,
                        ReflectionName = p.Name,
                        Title = fieldAttribute?.Title ?? p.Name,
                        TypeSetting = new AutoFieldTypeSetting(p.PropertyType, p.DeclaringType),
                        FuzzSetting = new AutoModelFuzzSetting(),
                        NavigationProperties = previousNavigateTuples
                    };
                    if (fromNavigateProperty != null)
                    {
                        // Treat ID properties specially; even when ignoring prefixes, keep the "ID" prefix.
                        if (p.Name.Equals("id", StringComparison.OrdinalIgnoreCase))
                        {
                            field.EnableIgnorePrefix = false;
                        }
                        else if (options.EnableIgnorePrefixAutoAdjust && allOriginActivateNames.Contains(p.Name))
                        {
                            field.EnableIgnorePrefix = false;
                        }
                    }


                    CheckFuzzSetting(field);

                    if (fieldAttribute != null)
                    {
                        if (fieldAttribute.ActivateNames != null)
                        {
                            activateNames.AddRange(fieldAttribute.ActivateNames);
                        }

                        field.FuzzSetting.IsIgnored = fieldAttribute.IgnoreFuzzColumn;
                    }


                    allOriginActivateNames.AddRange(activateNames);

                    if (activateNames.Count == 0)
                    {
                        var propertyName = field.DefaultActiveName;
                        activateNames.Add(propertyName.ToLowerInvariant()); // Normalized activation name used for case-insensitive matching.
                        allOriginActivateNames.Add(propertyName);
                    }


                    field.ActivateNames = [.. activateNames];
                    _snapshot.Fields.Add(field);
                    foreach (var name in activateNames)
                    {
                        if (!fieldDictionary.TryAdd(name, field))
                            throw new AutoModelSnapshotException(
                                displayMessage: "模型字段名称冲突，请使用 [AutoField] 特性重命名",
                                technicalDetail: $"类型: {table.FullTypeName}, 冲突字段: {field.ReflectionName}, 激活名: {name}, 已存在: {fieldDictionary[name].ReflectionName}");
                    }
                }
                catch (AutoModelSnapshotNotSupportTypeException)
                {
                    var declaringTypeName = p.DeclaringType?.GetCleanFullName() ?? "Unknown";
                    var propertyTypeName = p.PropertyType.GetCleanFullName();
                    var msg = $"AutoModel构建Snapshot时发现了不支持该字段类型：{propertyTypeName} in {declaringTypeName}";
                    if (options.EnableErrorForUnsupportedFieldTypes)
                    {
                        throw new AutoModelSnapshotException(
                            displayMessage: "模型包含不支持的字段类型",
                            technicalDetail: $"类型: {propertyTypeName}, 所在类: {declaringTypeName}");
                    }

                    Console.WriteLine(msg); // TODO: replace with the unified module logger later.
                }
            }

            _allActivateNames = allOriginActivateNames;
        }
    }

    /// <summary>
    /// Validates fuzzy-match support for a field.
    /// </summary>
    /// <param name="field">The field metadata to update.</param>
    private static void CheckFuzzSetting(AutoField field)
    {
        if (field.TypeSetting.TypeFeatures.HasAnyFlag(ETypeFeatures.IsCollection, ETypeFeatures.IsClass))
        {
            field.FuzzSetting.IsNotSupported = true;
        }
    }

    public IReadOnlyList<string> GetAllActivateNames()
    {
        return _allActivateNames;
    }

    public AutoField? GetField(string fieldActivateName)
    {
        return _fieldDictionary!.TryGetValue(fieldActivateName, out var field) ? field :
            _fieldDictionary.GetValueOrDefault(fieldActivateName.ToLowerInvariant());
    }

    public IReadOnlyList<AutoField> GetFields(IReadOnlyList<string>? fieldActivateNames = null)
    {
        if (fieldActivateNames is null) return _snapshot.Fields;
        var list = new List<AutoField>();
        foreach (var name in fieldActivateNames)
        {
            if (_fieldDictionary!.TryGetValue(name, out var field))
            {
                list.Add(field);
            }
        }
        return list;
    }
}
