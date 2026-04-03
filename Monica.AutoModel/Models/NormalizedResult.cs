using Monica.Tool.Diagnostics;

namespace Monica.AutoModel.Models;

public class NormalizedResult(string finalExpression, List<object?> @params, TokenizerContext context)
{
    public string FinalExpression { get; set; } = finalExpression;
    public List<object?> Params { get; set; } = @params;
    public TokenizerContext Context { get; } = context;

    public override string ToString()
    {
        return $"Generated expression:{FinalExpression}\nparams:{Params.Select((p, i) =>
            new
            {
                Index = $"@{i}",
                Type = p?.GetType().Name ?? "null",
                Value = p?.ToJsonString() ?? "null"
            }).ToJsonString()}";
    }
}
