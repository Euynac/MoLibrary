using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.Middlewares;

public class FilterSpecialCharacterConfig
{
    /// <summary>
    /// Gets or sets the custom special-character patterns to remove.
    /// </summary>
    public string SpecialCharacters { get; set; } = "";
}

public class FilterSpecialCharacterMiddleware(IOptions<FilterSpecialCharacterConfig> config) : PipelineTransformMiddlewareBase
{
    public override ChannelDataContext Pass(ChannelDataContext context)
    {
        if (context.Data is string dataStr)
        {
            // Remove HTML tags.
            dataStr = Regex.Replace(dataStr, "<.*?>", string.Empty);

            // Remove obvious JavaScript snippets.
            dataStr = Regex.Replace(dataStr, @"alert\s*\(.*?\)|eval\s*\(.*?\)", string.Empty, RegexOptions.IgnoreCase);

            // Remove caller-defined special characters or patterns.
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
