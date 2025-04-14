using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MoLibrary.DataChannel.Pipeline;

namespace MoLibrary.DataChannel.BuildInMiddlewares;

public class FilterSpecialCharacterConfig
{
    /// <summary>
    /// 自定义的特殊字符
    /// </summary>
    public string SpecialCharacters { get; set; } = "";
}

public class FilterSpecialCharacterMiddleware(IOptions<FilterSpecialCharacterConfig> config) : PipeTransformMiddlewareBase
{
    public override DataContext Pass(DataContext context)
    {
        if (context.Data is string dataStr)
        {
            //过滤HTML标签
            dataStr = Regex.Replace(dataStr, "<.*?>", string.Empty);

            //过滤JavaScript代码
            dataStr = Regex.Replace(dataStr, @"alert\s*\(.*?\)|eval\s*\(.*?\)", string.Empty, RegexOptions.IgnoreCase);

            //过滤自定义的特殊字符
            if (!string.IsNullOrEmpty(config.Value.SpecialCharacters))
            {
                foreach (var specialCharacter in config.Value.SpecialCharacters.Replace('，',',').Split(new[] {','},
                             StringSplitOptions.RemoveEmptyEntries))
                {
                    dataStr = Regex.Replace(dataStr, specialCharacter, string.Empty);
                }
            }
            context.Data = dataStr;
        }
        return base.Pass(context);
    }
}