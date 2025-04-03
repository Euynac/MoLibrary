using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.General;

namespace MoLibrary.AutoModel.Model;

public class FieldToken(string fieldStr, string conditionStr, string valueStr, int start, int end)
{
    /// <summary>
    /// 表达式中的字段激活名
    /// </summary>
    public string FieldStr { get; set; } = fieldStr;

    /// <summary>
    /// 表达式中的条件
    /// </summary>
    public string ConditionStr { get; set; } = conditionStr;

    /// <summary>
    /// 表达式中的值
    /// </summary>
    public string ValueStr { get; set; } = valueStr;

    /// <summary>
    /// 相应字段信息
    /// </summary>
    public AutoField? FieldInfo { get; set; }

    /// <summary>
    /// 字段条件
    /// </summary>
    public EFieldConditions Conditions { get; set; }

    /// <summary>
    /// 字段条件特性
    /// </summary>
    public EFieldConditionFeatures Features { get; set; }

    /// <summary>
    /// 转换后的值对象
    /// </summary>
    public object? ConvertedValue { get; set; }

    /// <summary>
    /// 位于原始表达式的开始位置
    /// </summary>
    public int Start { get; set; } = start;

    /// <summary>
    /// 位于原始表达式的结束位置
    /// </summary>
    public int End { get; set; } = end;

    public string? TokenExpression { get; set; }
    /// <summary>
    /// 获取字段属性参数
    /// </summary>
    /// <returns></returns>
    public string? GetFieldParam()
    {
        if (FieldInfo == null) return null;
        return FieldInfo.GetConditionExpressionParam();
    }

    public override string ToString()
    {
        return
            $"{FieldStr} {ConditionStr} \"{ValueStr}\" -> {FieldInfo} {Conditions} \"{ConvertedValue}\" {Features.GetFlagsString(ignoreEnums: EFieldConditionFeatures.None).BeIfNotEmpty(" with {0}", true)}{TokenExpression.BeIfNotEmpty(" => {0}", true)}";
    }
}

public class TokenizerContext(string originExpression)
{
    public string OriginExpression { get; set; } = originExpression;

    public List<FieldToken> Tokens { get; set; } = [];

    public string GetFinalExpression()
    {
        //TODO 优化同维度数据表达式
        var final = OriginExpression;
        for (var i = Tokens.Count - 1; i >= 0; i--)
        {
            var token = Tokens[i];
            if (token.TokenExpression is not { } tokenExp) continue;
            var start = token.Start;
            var end = token.End;
            final = final.Remove(start, end - start + 1).Insert(start, tokenExp);
        }

        return final;
    }

}

/// <summary>
/// 字段条件特性
/// </summary>
[Flags]
public enum EFieldConditionFeatures
{
    None,
    /// <summary>
    /// 正则表达式(暂未实现)
    /// </summary>
    UseRegex = 1 << 0,
    /// <summary>
    /// 需使用客户端侧评估。指明该字段无法被数据库直接处理，需要执行后在客户端进行处理(暂未实现)
    /// </summary>
    UseClientSideEvaluations = 1 << 1,
    /// <summary>
    /// 多选。默认当同时使用in 与,时将会启用
    /// </summary>
    Multi = 1 << 2,
    /// <summary>
    /// 模糊。默认当使用like时，且字段类型支持时将启用
    /// </summary>
    Fuzzy = 1 << 3,

    /// <summary>
    /// 非，当使用此枚举意味着该条件结果取反
    /// </summary>
    Not = 1 << 4,
}

/// <summary>
/// AutoModel字段条件
/// </summary>
public enum EFieldConditions
{
    /// <summary>
    /// 无条件
    /// </summary>
    None,
    /// <summary>
    /// 相等
    /// </summary>
    [KouEnumName("=")]
    Equal,
    /// <summary>
    /// 相似（string适用）比如使用了xx%
    /// </summary>
    [KouEnumName("like")]
    Like,
    /// <summary>
    /// 存在于
    /// </summary>
    [KouEnumName("in")]
    In,
    /// <summary>
    /// 大于
    /// </summary>
    [KouEnumName(">")]
    GreaterThan,
    /// <summary>
    /// 小于
    /// </summary>
    [KouEnumName("<")]
    LessThan,
    /// <summary>
    /// 大于等于
    /// </summary>
    [KouEnumName(">=")]
    GreaterThanOrEqual,
    /// <summary>
    /// 小于等于
    /// </summary>
    [KouEnumName("<=")]
    LessThanOrEqual,
    /// <summary>
    /// 不等于
    /// </summary>
    [KouEnumName("!=")]
    Unequal,
    /// <summary>
    /// 表达式相似
    /// </summary>
    [KouEnumName("explike")]
    ExpLike,
    /// <summary>
    /// 不相似
    /// </summary>
    [KouEnumName("notlike")]
    NotLike,
    /// <summary>
    /// 是 专门用于判断是空的、不是空的等
    /// </summary>
    [KouEnumName("is")]
    Is,
}