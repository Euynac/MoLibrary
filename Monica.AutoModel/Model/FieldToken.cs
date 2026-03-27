using Monica.Tool.Extensions;
using Monica.Tool.General;

namespace Monica.AutoModel.Model;

public class FieldToken(string fieldStr, string conditionStr, string valueStr, int start, int end)
{
    /// <summary>
    /// Field activation name in the expression.
    /// </summary>
    public string FieldStr { get; set; } = fieldStr;

    /// <summary>
    /// Condition token in the expression.
    /// </summary>
    public string ConditionStr { get; set; } = conditionStr;

    /// <summary>
    /// Value token in the expression.
    /// </summary>
    public string ValueStr { get; set; } = valueStr;

    /// <summary>
    /// Resolved field metadata.
    /// </summary>
    public AutoField? FieldInfo { get; set; }

    /// <summary>
    /// Parsed field condition.
    /// </summary>
    public EFieldConditions Conditions { get; set; }

    /// <summary>
    /// Parsed field-condition features.
    /// </summary>
    public EFieldConditionFeatures Features { get; set; }

    /// <summary>
    /// Converted value object.
    /// </summary>
    public object? ConvertedValue { get; set; }

    /// <summary>
    /// Start position in the original expression.
    /// </summary>
    public int Start { get; set; } = start;

    /// <summary>
    /// End position in the original expression.
    /// </summary>
    public int End { get; set; } = end;

    public string? TokenExpression { get; set; }
    /// <summary>
    /// Gets the field parameter used in the generated condition expression.
    /// </summary>
    /// <returns>The field parameter expression, or <c>null</c> if the field is unresolved.</returns>
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
        // TODO: Optimize expressions built from tokens at the same logical level.
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
/// Field-condition features.
/// </summary>
[Flags]
public enum EFieldConditionFeatures
{
    None,
    /// <summary>
    /// Regular expression matching. Not implemented yet.
    /// </summary>
    UseRegex = 1 << 0,
    /// <summary>
    /// Requires client-side evaluation because the field cannot be handled directly by the database. Not implemented yet.
    /// </summary>
    UseClientSideEvaluations = 1 << 1,
    /// <summary>
    /// Multi-value mode. Enabled by default when both <c>in</c> and <c>,</c> are used.
    /// </summary>
    Multi = 1 << 2,
    /// <summary>
    /// Fuzzy mode. Enabled by default when <c>like</c> is used and the field type supports it.
    /// </summary>
    Fuzzy = 1 << 3,

    /// <summary>
    /// Negation. When this flag is set, the condition result is inverted.
    /// </summary>
    Not = 1 << 4,
}

/// <summary>
/// AutoModel field conditions.
/// </summary>
public enum EFieldConditions
{
    /// <summary>
    /// No condition.
    /// </summary>
    None,
    /// <summary>
    /// Equal to.
    /// </summary>
    [KouEnumName("=")]
    Equal,
    /// <summary>
    /// Pattern match for strings, such as <c>xx%</c>.
    /// </summary>
    [KouEnumName("like")]
    Like,
    /// <summary>
    /// Exists within.
    /// </summary>
    [KouEnumName("in")]
    In,
    /// <summary>
    /// Greater than.
    /// </summary>
    [KouEnumName(">")]
    GreaterThan,
    /// <summary>
    /// Less than.
    /// </summary>
    [KouEnumName("<")]
    LessThan,
    /// <summary>
    /// Greater than or equal to.
    /// </summary>
    [KouEnumName(">=")]
    GreaterThanOrEqual,
    /// <summary>
    /// Less than or equal to.
    /// </summary>
    [KouEnumName("<=")]
    LessThanOrEqual,
    /// <summary>
    /// Not equal to.
    /// </summary>
    [KouEnumName("!=")]
    Unequal,
    /// <summary>
    /// Expression-based pattern match.
    /// </summary>
    [KouEnumName("explike")]
    ExpLike,
    /// <summary>
    /// Not like.
    /// </summary>
    [KouEnumName("notlike")]
    NotLike,
    /// <summary>
    /// Identity check, primarily used for null and non-null checks.
    /// </summary>
    [KouEnumName("is")]
    Is,
}
