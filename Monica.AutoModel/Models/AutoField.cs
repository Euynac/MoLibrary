using System.Text;
using System.Text.Json.Serialization;
using Monica.Modules;
using Monica.Tool.Extensions;

namespace Monica.AutoModel.Models;

/// <summary>
/// AutoModel field metadata.
/// </summary>
public class AutoField
{
    /// <summary>
    /// Activation names that can be used to reference this field.
    /// </summary>
    public HashSet<string> ActivateNames { get; set; } = [];

    #region Navigation

    /// <summary>
    /// Navigation-property path segments from the root model to this field,
    /// together with a flag indicating whether each segment is an <c>ICollection</c>.
    /// </summary>
    [JsonIgnore]
    public List<(string RefelectName, bool IsCollection)>? NavigationProperties { get; set; }

    [JsonInclude]
    private string? NavigationPropertiesFormat =>
        NavigationProperties?.Select(p => p.IsCollection ? $"List<{p.RefelectName}>" : p.RefelectName).StringJoin('.');


    #region DynamicLinq Mode 1

    public string GetConditionExpressionParam()
    {
        if (NavigationProperties is null) return ReflectionName;
        var sb = new StringBuilder().Append(ReflectionName);
        for (var i = NavigationProperties.Count - 1; i >= 0; i--)
        {
            var (navigation, isCollection) = NavigationProperties[i];
            if (isCollection) break;
            sb.Insert(0, $"{navigation}.");
        }

        return sb.ToString();
    }

    public string BlendNavigationParam(string conditionExpression)
    {
        if (NavigationProperties is null) return conditionExpression;
        return CreateNavigationExpression();

        string CreateNavigationExpression(string? prefix = null, int navigationIndex = 0)
        {
            var navigationInfo = NavigationProperties.ElementAtOrDefault(navigationIndex++);
            var navigationName = navigationInfo.RefelectName;
            var isCollection = navigationInfo.IsCollection;
            if (navigationInfo == default) // Reached the actual field; no navigation segment remains.
            {
                var fieldName = ReflectionName;
                if ((TypeSetting.TypeFeatures & ETypeFeatures.IsCollection) != 0)
                {

                    return $"{fieldName} != null && {fieldName}.Any({conditionExpression})";
                }

                return conditionExpression;
            }

            var curObj = prefix?.Be($"{prefix}.{navigationName}") ?? navigationName;
            if (isCollection)
            {
                return
                    $"{curObj} != null && {curObj}.Any({CreateNavigationExpression(curObj, navigationIndex)})";
            }

            return CreateNavigationExpression(curObj, navigationIndex);
        }
    }

    #endregion

    #region DynamicLinq Mode 2 (lambda mode) https://dynamic-linq.net/basic-simple-query#more-where-examples

    //public string GetConditionExpressionParam(string finalItemVar = "i")
    //{
    //    if(NavigationProperties is null) return ReflectionName;
    //    var sb = new StringBuilder().Append(ReflectionName);
    //    for (var i = NavigationProperties.Count - 1; i >= 0; i--)
    //    {
    //        var (navigation, isCollection) = NavigationProperties[i];
    //        if(isCollection)
    //        {
    //            return sb.Insert(0, $"{finalItemVar}.").ToString();
    //        }

    //        sb.Insert(0, $"{navigation}.");
    //    }

    //    return sb.ToString();
    //}

    //public string BlendNavigationParam(string conditionExpression, string finalItemVar = "i")
    //{
    //    if (NavigationProperties is null) return conditionExpression;
    //    return CreateNavigationExpression();

    //    string CreateNavigationExpression(string? prefix = null, int iteratorCount = 0,
    //        int navigationIndex = 0)
    //    {
    //        var navigationInfo = NavigationProperties.ElementAtOrDefault(navigationIndex++);
    //        var navigationName = navigationInfo.Item1;
    //        var isCollection = navigationInfo.Item2;
    //        if (navigationInfo == default) // Reached the actual field; no navigation property remains.
    //        {
    //            var fieldName = ReflectionName;
    //            if ((TypeSetting.TypeFeatures & ETypeFeatures.IsCollection) != 0)
    //            {

    //                return $"{fieldName} != null && {fieldName}.Any({finalItemVar} => {conditionExpression})";
    //            }

    //            return conditionExpression;
    //        }

    //        var curObj = prefix?.Be($"{prefix}.{navigationName}") ?? navigationName;
    //        if (isCollection)
    //        {
    //            var collectionItem = NavigationProperties.ElementAtOrDefault(navigationIndex + 1) == default
    //                ? finalItemVar
    //                : "i" + ++iteratorCount;
    //            return
    //                $"{curObj} != null && {curObj}.Any({collectionItem} => {CreateNavigationExpression(collectionItem, iteratorCount, navigationIndex)})";
    //        }

    //        return CreateNavigationExpression(curObj, iteratorCount, navigationIndex);
    //    }
    //}

    #endregion



    #endregion
    /// <summary>
    /// Display name of the field. Defaults to the reflected property name.
    /// </summary>
    public required string Title { get; set; }
    /// <summary>
    /// Reflected property name.
    /// </summary>
    public required string ReflectionName { get; set; }

    /// <summary>
    /// Fuzzy-match configuration.
    /// </summary>
    public required AutoModelFuzzSetting FuzzSetting { get; set; }

    /// <summary>
    /// Field type metadata.
    /// </summary>
    public required AutoFieldTypeSetting TypeSetting { get; set; }
    /// <summary>
    /// <inheritdoc cref="ModuleAutoModelOption.EnableIgnorePrefix"/>
    /// </summary>
    public bool EnableIgnorePrefix { get; set; }

    /// <summary>
    /// Gets the default activation name for this field.
    /// </summary>
    public string DefaultActiveName =>
        EnableIgnorePrefix ? ReflectionName : $"{NavigationProperties?.Select(s => s.RefelectName).StringJoin(".").BeIfNotEmpty("{0}.", true)}{ReflectionName}";
    /// <summary>
    /// Indicates that this field must be evaluated on the client because it cannot be translated to SQL.
    /// </summary>
    [Obsolete("Not implemented yet.")]
    public bool ShouldUseClientEvaluation { get; set; }
    public override string ToString()
    {
        return $"{NavigationPropertiesFormat?.BeIfNotEmpty("{0}.", true)}{ReflectionName}({string.Join(',', ActivateNames)})[{TypeSetting}]";
    }
}
