using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoLibrary.AutoModel.AutoModel.Implements;
using MoLibrary.AutoModel.Configurations;
using MoLibrary.AutoModel.Interfaces;
using MoLibrary.AutoModel.Model;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.General;
using System.Text;
using MoLibrary.AutoModel.Exceptions;

namespace MoLibrary.AutoModel.Implements;

public class AutoModelExpressionNormalizerDynamicLinqProvider<TModel>(
    IAutoModelSnapshot<TModel> snapshot,
    IOptions<AutoModelExpressionOptions> expressionOptions,
    IOptions<ModuleOptionAutoModel> options,
    IAutoModelExpressionTokenizer<TModel> tokenizer,
    ILogger<AutoModelExpressionTokenizer<TModel>> logger)
    : IAutoModelExpressionNormalizer<TModel>
{
    protected AutoModelExpressionOptions ExpressionOptions = expressionOptions.Value;
    protected ModuleOptionAutoModel Options = options.Value;
    protected bool LinqToObject = false;

    private List<AutoField> NormalizeLiteralSelect(string columns)
    {
        var names = new List<AutoField>();
        var errors = new StringBuilder();
        foreach (var column in columns.Split(ExpressionOptions.SelectSeparator,
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (snapshot.GetField(column) is { } name)
            {
                names.Add(name);
            }
            else
            {
                errors.Append($"{column},");
            }
        }

        if (errors.Length <= 0) return names;
        errors.Remove(errors.Length - 1, 1);
        throw new AutoModelNormalizeException($"选择字段{errors}无法识别。支持的激活名有：{string.Join(',', snapshot.GetAllActivateNames())}");
    }

    /// <summary>
    /// 获取字段的选择表达式
    /// </summary>
    /// <param name="field"></param>
    /// <returns></returns>
    private string GetSelectExpression(AutoField field)
    {
        if (field.NavigationProperties is { } list)
        {
            if (field.NavigationProperties.Any(p => p.IsCollection)) throw new AutoModelNormalizeException($"暂不支持选择列表字段{field}");

            return list.Select(p => p.RefelectName).CombineForeach([field.ReflectionName]).StringJoin(".");
        }
        return field.ReflectionName;
    }

    public string NormalizeSelectColumns(string selectColumns, bool isReverseSelect = false)
    {
        if (string.IsNullOrWhiteSpace(selectColumns))
            throw new AutoModelNormalizeException("选择字段不能为空");
        var selectColumnExp = "";
        if (isReverseSelect)
        {
            var excepted = NormalizeLiteralSelect(selectColumns).Select(GetSelectExpression).ToHashSet();
            var all = snapshot.GetFields().Select(GetSelectExpression).ToHashSet();
            selectColumnExp = all.Except(excepted).StringJoin(",");
        }
        else
        {
            selectColumnExp = NormalizeLiteralSelect(selectColumns).Select(GetSelectExpression).StringJoin(",");
        }
        var expression = $"new {{ {selectColumnExp} }}";
        if (Options.EnableDebugging)
        {
            logger.LogInformation("Select string: {filter}", expression);
        }

        return expression;
    }

    public NormalizedResult NormalizeFilter(string filter)
    {
        var context = tokenizer.Tokenize(filter);
        foreach (var token in context.Tokens)
        {
            if (LinqToObject)
            {
                token.Features |= EFieldConditionFeatures.UseClientSideEvaluations;
            }
        }
        var result = tokenizer.GenFinalExpression(context);
        if (Options.EnableDebugging)
        {
            logger.LogInformation("Filter string: {filter}", context.OriginExpression);
            logger.LogInformation(result.ToString());
        }

        return result;
    }

    public NormalizedResult NormalizeFuzzy(string fuzzy, string? fuzzyColumns = null)
    {
        //TODO 检查是否包含""、括号等注入
        List<AutoField> fuzzyColumnsList;
        if (!string.IsNullOrWhiteSpace(fuzzyColumns))
        {
            fuzzyColumnsList = NormalizeLiteralSelect(fuzzyColumns);
            var unsupported = fuzzyColumnsList.Where(p => p.FuzzSetting.IsNotSupported)
                .Select(p => $"({p.TypeSetting.OriginType.Name}){p.ReflectionName}").StringJoin(",");
            if (!string.IsNullOrWhiteSpace(unsupported))
            {
                throw new AutoModelNormalizeException($"模糊查询存在不支持的字段：{unsupported}");
            }
        }
        else
        {
            fuzzyColumnsList = snapshot.GetFields().Where(x => x.FuzzSetting is { IsIgnored: false, IsNotSupported: false }).ToList();
        }

        //TODO improve performance
        var context = tokenizer.Tokenize(fuzzyColumnsList.Select(p => $"({p.DefaultActiveName} like \"{fuzzy}\")").StringJoin(" or "));
        var result = tokenizer.GenFinalExpression(context);
        if (Options.EnableDebugging)
        {
            logger.LogInformation("Filter string: {filter}", context.OriginExpression);
            logger.LogInformation("Generated expression: {expression}", result.FinalExpression);
            logger.LogInformation("value: \n{value}", result.Params.ToJsonString());
        }

        return result;
    }

    public void SetToLinqToObject()
    {
        LinqToObject = true;
    }
}