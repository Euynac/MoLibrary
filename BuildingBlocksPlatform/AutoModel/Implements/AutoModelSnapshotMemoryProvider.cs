using System.Collections;
using System.Collections.Frozen;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.Json.Serialization;
using BuildingBlocksPlatform.AutoModel.Annotations;
using BuildingBlocksPlatform.AutoModel.Configurations;
using BuildingBlocksPlatform.AutoModel.Exceptions;
using BuildingBlocksPlatform.AutoModel.Interfaces;
using BuildingBlocksPlatform.AutoModel.Model;
using BuildingBlocksPlatform.Extensions;
using MoLibrary.Tool.Extensions;
using Microsoft.Extensions.Options;

// ReSharper disable StaticMemberInGenericType

namespace BuildingBlocksPlatform.AutoModel.Implements;

/// <summary>
/// AutoModel内存快照服务
/// </summary>
/// <typeparam name="TModel"></typeparam>
public class AutoModelSnapshotMemoryProvider<TModel> : IAutoModelSnapshot<TModel>
{
    private static AutoModelSnapshot _snapshot = null!;
    private static FrozenDictionary<string, AutoField>? _fieldDictionary;
    private static IReadOnlyList<string> _allActivateNames = null!;

    public AutoModelSnapshotMemoryProvider(IOptions<AutoModelOptions> options)
    {
        Init(options.Value);
    }

    private static IEnumerable<PropertyInfo> GetAutoFieldTypes(Type type)
    {
        //巨坑：string也是Class，引用类型。string?本质上是加了一个NullableContextAttribute. 所以string?和string可用typeof(string)统一判断。
        return type.GetProperties().OrderByDescending(p => p.PropertyType == typeof(string)).ThenBy(p => p.PropertyType.IsClass);
    }

    private static void Init(AutoModelOptions options)
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
        var isActiveMode = options.ActiveMode;
        var tableAttribute = typeof(TModel).GetCustomAttribute<AutoTableAttribute>();
        if (tableAttribute != null)
        {
            _snapshot.Table.Name = tableAttribute.Name;
            isActiveMode = tableAttribute.ActiveMode ?? isActiveMode;
        }

        var navigatedTypeNames = new HashSet<Type>();//防止嵌套造成的栈溢出。仅支持同种类型深入一层调用。
        List<string> allOriginActivateNames = [];

        ExtractFieldInfo(GetAutoFieldTypes(typeof(TModel)));
        _fieldDictionary = fieldDictionary.ToFrozenDictionary();
        AutoModelSnapshotFactoryMemoryProvider.Snapshots.Add(_snapshot);
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
                //如果是主动模式，但是没有设置AutoFieldAttribute，则跳过
                if (isActiveMode && fieldAttribute == null) continue;
                if (fieldAttribute?.Ignore is true) continue;
                //如果未使用AutoField标签，且使用NotMapped, JsonIgnore的也自动忽略
                if (fieldAttribute is null)
                {
                    if (p.GetCustomAttribute<NotMappedAttribute>() != null) continue;
                    if (p.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;
                }
                

                //字典类型不支持
                if (typeof(IDictionary).IsAssignableFrom(p.PropertyType)) continue;

                //判断是否是导航属性
                if (p.PropertyType.GetGenericUnderlyingType() is { IsClass: true } underlyingType && underlyingType != typeof(string))
                {
                    if (!navigatedTypeNames.Add(underlyingType)) continue;
                    ExtractFieldInfo(GetAutoFieldTypes(underlyingType), p, previousNavigateTuples?.ToList());
                    continue;
                }

                var activateNames = new List<string>();

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
                    //对于ID字段特殊处理，即使开启了忽略前缀，也需要保留ID前缀
                    if (p.Name.Equals("id", StringComparison.OrdinalIgnoreCase))
                    {
                        field.EnableIgnorePrefix = false;
                    }
                    else if (options.EnableIgnorePrefixAutoAdjust && allOriginActivateNames.Contains(p.Name))
                    {
                        field.EnableIgnorePrefix = false;
                    }
                }


                CheckFuzzSetting(p, field);

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
                    activateNames.Add(propertyName.ToLowerInvariant());//正规化后的激活名，用以忽略大小写匹配
                    allOriginActivateNames.Add(propertyName);
                }


                field.ActivateNames = [.. activateNames];
                _snapshot.Fields.Add(field);
                foreach (var name in activateNames)
                {
                    if (!fieldDictionary.TryAdd(name, field))
                        throw new AutoModelSnapshotException($"{table.FullTypeName}中{field}激活名{name}已存在，存在的是{fieldDictionary[name]}");
                }
            }

            _allActivateNames = allOriginActivateNames;
        }
    }

    /// <summary>
    /// 检查字段模糊设置
    /// </summary>
    /// <param name="propertyInfo"></param>
    /// <param name="fieldSetting"></param>
    private static void CheckFuzzSetting(PropertyInfo propertyInfo, AutoField fieldSetting)
    {
        if (fieldSetting.TypeSetting.TypeFeatures.HasAnyFlag(ETypeFeatures.IsCollection, ETypeFeatures.IsClass))
        {
            fieldSetting.FuzzSetting.IsNotSupported = true;
        }
    }

    public IReadOnlyList<string> GetAllActivateNames()
    {
        return _allActivateNames;
    }

    public AutoField? GetField(string fieldActivateName)
    {
        return _fieldDictionary!.TryGetValue(fieldActivateName, out var field) ? field :
            _fieldDictionary.TryGetValue(fieldActivateName.ToLowerInvariant(), out var field2) ? field2 : null;
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

public class AutoModelSnapshotFactoryMemoryProvider : IAutoModelSnapshotFactory
{
    internal static readonly List<AutoModelSnapshot> Snapshots;

    static AutoModelSnapshotFactoryMemoryProvider()
    {
        Snapshots = [];
    }
    public IReadOnlyList<AutoModelSnapshot> GetSnapshots()
    {
        return Snapshots;
    }

}