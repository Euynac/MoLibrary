using Microsoft.Extensions.Options;
using MoLibrary.AutoModel.Configurations;
using MoLibrary.AutoModel.Implements;
using MoLibrary.AutoModel.Interfaces;
using MoLibrary.AutoModel.Model;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using MoLibrary.AutoModel.Exceptions;

namespace MoLibrary.AutoModel.AutoModel.Implements;

public partial class AutoModelTokenExpressionGenDynamicLinqProvider(IOptions<AutoModelExpressionOptions> expressionOptions) : IAutoModelTokenExpressionGen
{
    protected AutoModelExpressionOptions ExpressionOptions = expressionOptions.Value;
    private AutoFieldTypeSetting _fieldTypeSetting = null!;
    private EFieldConditionFeatures _features;
    private FieldToken _token = null!;
    private object? _curValue = null!;
    /// <summary>
    /// 指代表达式中值属性
    /// </summary>
    private string _curValueParam = null!;
    /// <summary>
    /// 指代当前字段属性
    /// </summary>
    private string _curFieldParam = null!;
    private int _curTotalParamCount = 0;
    private EFieldConditions _conditions;
    private readonly List<object> _supplementParameterObjects = [];
    private bool _isFuzzy => (_features & EFieldConditionFeatures.Fuzzy) != 0;
    private bool _isMulti => (_features & EFieldConditionFeatures.Multi) != 0;

    /// <summary>
    /// 目前不支持动态扩展LinqToSql的方法
    /// </summary>
    protected virtual string LinqFunctions => (_features & EFieldConditionFeatures.UseClientSideEvaluations) != 0
        ? nameof(LinqToObjectFunctions)
        : "EF.Functions";

    /// <summary>
    /// 判断ConvertedValue特殊属性，是否需要提前结束生成
    /// </summary>
    /// <param name="expression"></param>
    /// <returns></returns>
    private bool ShouldBreakConvertedValue([NotNullWhen(true)] out string? expression)
    {
        expression = null;
        if (_curValue is ResultJumpThisField)
        {
            expression = "false";
            return true;
        }

        return false;
    }

    public string GenerateTokenExpression(FieldToken token, int num, int totalParamCount, out List<object> supplementParamObjects)
    {
        _curValue = token.ConvertedValue;
        supplementParamObjects = _supplementParameterObjects;
        if (ShouldBreakConvertedValue(out var result)) return result;

        _token = token;
        _fieldTypeSetting = token.FieldInfo!.TypeSetting;
        _conditions = token.Conditions;
        _features = token.Features;
        _curFieldParam = token.GetFieldParam()!;
        _curTotalParamCount = totalParamCount;
        //https://github.com/zzzprojects/System.Linq.Dynamic.Core/issues/440
        //_curValueParam = _isMulti ? "it" : $"@{num}";
        _curValueParam = $"@{num}";
        if (_isMulti && _curValue is IList)
        {
            return $"{_curValueParam}.Contains({_curFieldParam})";
        }
        //use https://eval-expression.net/linq-dynamic instead?

        if (token.Conditions == EFieldConditions.Is)
        {
            return ResolveIs();
        }

        if (token.Conditions == EFieldConditions.ExpLike)
        {
            return ResolveExpLike();
        }


        var basicType = _fieldTypeSetting.BasicType;
        var tokenExpression = basicType switch
        {
            EBasicType.IsString => GenForString(),
            EBasicType.IsInt => GenForInt(),
            EBasicType.IsLong => GenForLong(),
            EBasicType.IsDouble => GenForDouble(),
            EBasicType.IsBoolean => GenForBoolean(),
            EBasicType.IsDateTime => GenForDateTime(),
            EBasicType.IsTimeOnly => GenForTimeOnly(),
            EBasicType.IsDateOnly => GenForDateOnly(),
            EBasicType.IsTimeSpan => GenForTimeSpan(),
            EBasicType.IsEnum => GenForEnum(),
            EBasicType.IsGuid => GenForGuid(),
            _ => GenByGeneral()
        };

        //return _isMulti ? $"@{num}.Any({tokenExpression})" : tokenExpression;
        return tokenExpression;
    }
    private dynamic GenByGeneral()
    {
        if (_isFuzzy) GenByFuzzyGeneral();
        return GetGeneralRelationExpression();
    }

    private dynamic GetGeneralRelationExpression(string? fieldParam = null, EFieldConditions? conditions = null, string? valueParam = null)
    {
        fieldParam ??= _curFieldParam;
        conditions ??= _conditions;
        valueParam ??= _curValueParam;

        return conditions switch
        {
            EFieldConditions.Equal => $"{fieldParam} == {valueParam}",
            EFieldConditions.Unequal => $"{fieldParam} != {valueParam}",
            EFieldConditions.GreaterThan => $"{fieldParam} > {valueParam}",
            EFieldConditions.GreaterThanOrEqual => $"{fieldParam} >= {valueParam}",
            EFieldConditions.LessThan => $"{fieldParam} < {valueParam}",
            EFieldConditions.LessThanOrEqual => $"{fieldParam} <= {valueParam}",
            EFieldConditions.In => $"{fieldParam} == {valueParam}",
            _ => throw new ArgumentOutOfRangeException(nameof(conditions), conditions, null)
        };
    }

    private dynamic GenByFuzzyGeneral()
    {
        return $"{_curFieldParam}.ToString().Contains({_curValueParam})";
    }


    private dynamic GenForBoolean()
    {
        if (_isFuzzy)
        {
            if (_curValue is not bool) return GenByFuzzyGeneral();
        }

        var condition = _conditions == EFieldConditions.Unequal ? "!=" : "==";
        return $"{_curFieldParam} {condition} {_curValueParam}";
    }

    #region DateTime相关

    private dynamic GenForTimeSpan()
    {
        if (_isFuzzy) return GenByFuzzyGeneral();
        return GetGeneralRelationExpression();
    }

    private dynamic GenForDateTime()
    {
        if (_isFuzzy) return GenByFuzzyGeneral();
        return GetGeneralRelationExpression();
    }
    private dynamic GenForDateOnly()
    {
        if (_isFuzzy) return GenByFuzzyGeneral();
        return GetGeneralRelationExpression();
    }
    private dynamic GenForTimeOnly()
    {
        if (_isFuzzy)
        {
            if (_curValue is ObjectInterval interval)
            {
                return GenByInterval(interval);
            }

            return GetGeneralRelationExpression();
        }
        return GetGeneralRelationExpression();
    }

    #endregion

    #region Numeric相关

    private dynamic GenForDouble()
    {
        if (_isFuzzy) return GenByFuzzyGeneral();
        return GetGeneralRelationExpression();
    }

    private dynamic GenForLong()
    {
        if (_isFuzzy) return GenByFuzzyGeneral();
        return GetGeneralRelationExpression();
    }

    private dynamic GenForInt()
    {
        if (_isFuzzy) return GenByFuzzyGeneral();
        return GetGeneralRelationExpression();
    }

    #endregion

    private dynamic GenForEnum()
    {
        var condition = _conditions;
        if (_isFuzzy)
        {
            condition = EFieldConditions.Equal;
        }
        return GetGeneralRelationExpression(_curFieldParam, condition, _curValueParam);
    }

    private dynamic GenForString()
    {
        if (_isFuzzy) return $"{LinqFunctions}.Like({_curFieldParam}, {_curValueParam})";
        var condition = _conditions == EFieldConditions.Unequal ? "!=" : "==";
        return $"{_curFieldParam} {condition} {_curValueParam}";
    }

    private dynamic GenForGuid()
    {
        if (_isFuzzy) return GenByFuzzyGeneral();
        var condition = _conditions == EFieldConditions.Unequal ? "!=" : "==";
        return $"{_curFieldParam} {condition} {_curValueParam}";
    }

    /// <summary>
    /// 补充附加的参数，并输出其所在序号
    /// </summary>
    /// <param name="supplement"></param>
    /// <returns></returns>
    private int SupplementParameter(object supplement)
    {
        _supplementParameterObjects.Add(supplement);
        return _curTotalParamCount + _supplementParameterObjects.Count - 1;
    }

    private string GenByInterval(ObjectInterval interval)
    {
        var leftCondition = "";
        var rightCondition = "";
        if (interval is { LeftNotLimit: true, RightNotLimit: true }) return "true";

        if (!interval.LeftNotLimit)
        {
            var compare = interval.IsLeftOpen is true ? ">" : ">=";
            leftCondition = $"{_curFieldParam} {compare} {SupplementParameter(interval.LeftValue)}";
        }

        if (!interval.RightNotLimit)
        {
            var compare = interval.IsRightOpen is true ? "<" : "<=";
            rightCondition = $"{_curFieldParam} {compare} {SupplementParameter(interval.RightValue)}";
        }

        return interval is { RightNotLimit: false, LeftNotLimit: false }
            ? $"{leftCondition} && {rightCondition}"
            : $"{leftCondition}{rightCondition}";
    }

    #region Is

    /// <summary>
    /// 解析Is表达式
    /// </summary>
    /// <returns></returns>
    private string ResolveIs()
    {
        if (_curValue is string exp && !string.IsNullOrWhiteSpace(exp))
        {
            var parsed = exp.ToLowerInvariant().Trim();
            var not = false;
            if (parsed.StartsWith("not"))
            {
                not = true;
                parsed = parsed[3..].TrimStart();
            }
            if (parsed == "null")
            {
                return not ? $"{_curFieldParam} != null" : $"{_curFieldParam} == null";
            }
        }

        throw new AutoModelTokenExpGenException("生成Is对应表达式错误");
    }

    #endregion


    #region ExpLike
    /// <summary>
    /// 解析 ExpLike 表达式
    /// </summary>
    /// <returns></returns>
    private string ResolveExpLike()
    {
        if (_curValue is string exp && !string.IsNullOrWhiteSpace(exp))
        {
            var regex = ExpLikeExpressionRegex();
            var finalExp = new StringBuilder();
            int? lastIndex = null;
            if (!regex.IsMatch(exp))
            {
                return GenElementExp(0, exp.Length);
            }

            foreach (Match match in regex.Matches(exp))
            {
                lastIndex ??= -1;
                var start = lastIndex.Value + 1;
                var end = match.Index;
                var elementExp = GenElementExp(start, end);
                finalExp.Append(elementExp);
                lastIndex = match.Index;

                if (match.Value is { } value)
                {
                    if (value.Equals(ExpressionOptions.ExpLikeAnd))
                    {
                        finalExp.Append(ExpressionOptions.And);
                    }
                    else if (value.Equals(ExpressionOptions.ExpLikeOr))
                    {
                        finalExp.Append(ExpressionOptions.Or);
                    }
                    else
                    {
                        finalExp.Append(value);
                    }
                }
            }

            if (lastIndex != null && lastIndex + 1 < exp.Length)
            {
                finalExp.Append(GenElementExp(lastIndex.Value + 1, exp.Length));
            }

            return finalExp.ToString();
        }

        throw new AutoModelTokenExpGenException("生成ExpLike对应表达式错误");

        string GenElementExp(int start, int end)
        {
            var input = exp[start..end].Trim();
            if (!string.IsNullOrEmpty(input))
            {
                var isNot = false;
                if (input.StartsWith(ExpressionOptions.ExpLikeNot))
                {
                    input = input.Replace(ExpressionOptions.ExpLikeNot, "").Trim();
                    isNot = true;
                }

                var condition = $"{LinqFunctions}.Like({_curFieldParam}, @{SupplementParameter(input.Contains(ExpressionOptions.Fuzzy) ? input : $"%{input}%")})";
                if (isNot)
                {
                    condition = $"!{condition}";
                }

                return $" {condition} ";
            }

            return " ";
        }
    }

    /// <summary>
    /// ExpLike正则
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex("""
                    [\|\(\)\&]
                    """, RegexOptions.Compiled)]
    private static partial Regex ExpLikeExpressionRegex();


    #endregion
}