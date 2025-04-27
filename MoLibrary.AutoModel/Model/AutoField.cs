using MoLibrary.AutoModel.Configurations;
using MoLibrary.Tool.Extensions;
using System.Text;
using System.Text.Json.Serialization;

namespace MoLibrary.AutoModel.Model;

/// <summary>
/// AutoModel 字段设置
/// </summary>
public class AutoField
{
    /// <summary>
    /// 字段激活名
    /// </summary>
    public HashSet<string> ActivateNames { get; set; }

    #region Navigation

    /// <summary>
    /// 导航属性前缀（即从主类导航到该属性的反射名前缀）与指示导航属性是否是ICollection的元组列表
    /// </summary>
    [JsonIgnore]
    public List<(string RefelectName, bool IsCollection)>? NavigationProperties { get; set; }

    [JsonInclude]
    private string? NavigationPropertiesFormat =>
        NavigationProperties?.Select(p => p.IsCollection ? $"List<{p.RefelectName}>" : p.RefelectName).StringJoin('.');


    #region DynamicLinq模式一

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
            if (navigationInfo == default) //到最后本身字段，没有导航属性了
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

    #region DynamicLinq模式二（lambda模式）https://dynamic-linq.net/basic-simple-query#more-where-examples

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
    //        if (navigationInfo == default) //到最后本身字段，没有导航属性了
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
    /// 字段显示名，没有默认是反射名
    /// </summary>
    public required string Title { get; set; }
    /// <summary>
    /// 字段反射名
    /// </summary>
    public required string ReflectionName { get; set; }

    /// <summary>
    /// 模糊查询设置
    /// </summary>
    public required AutoModelFuzzSetting FuzzSetting { get; set; }

    /// <summary>
    /// 字段类型设置
    /// </summary>
    public required AutoFieldTypeSetting TypeSetting { get; set; }
    /// <summary>
    /// <inheritdoc cref="ModuleOptionAutoModel.EnableIgnorePrefix"/>
    /// </summary>
    public bool EnableIgnorePrefix { get; set; }

    /// <summary>
    /// 获取默认激活名
    /// </summary>
    public string DefaultActiveName =>
        EnableIgnorePrefix ? ReflectionName : $"{NavigationProperties?.Select(s => s.RefelectName).StringJoin(".").BeIfNotEmpty("{0}.", true)}{ReflectionName}";
    /// <summary>
    /// 该字段需适用客户端侧评估（无法翻译为SQL）
    /// </summary>
    [Obsolete("暂未实现")]
    public bool ShouldUseClientEvaluation { get; set; }
    public override string ToString()
    {
        return $"{NavigationPropertiesFormat?.BeIfNotEmpty("{0}.", true)}{ReflectionName}({string.Join(',', ActivateNames)})[{TypeSetting}]";
    }
}