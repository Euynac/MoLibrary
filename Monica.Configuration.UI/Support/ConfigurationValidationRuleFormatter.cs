using Microsoft.Extensions.Localization;
using Monica.Configuration.Models;
using Monica.Configuration.Serialization;
using Monica.Configuration.UI.Localization;

namespace Monica.Configuration.UI.Support;

/// <summary>
/// Formats configuration validation rules for operator-facing rule tips.
/// </summary>
public static class ConfigurationValidationRuleFormatter
{
    /// <summary>
    /// Formats one validation rule.
    /// </summary>
    /// <param name="rule">The rule to format.</param>
    /// <param name="localizer">The localizer used for user-facing text.</param>
    /// <returns>A localized rule explanation.</returns>
    public static string Format(ConfigurationValidationRule rule, IStringLocalizer<ConfigurationUIResource> localizer)
    {
        return rule switch
        {
            RequiredRule => localizer["ValidationRules:Required"],
            RangeRule rangeRule => localizer["ValidationRules:Range", DisplayBound(rangeRule.Min, localizer["ValidationRules:NoMinimum"]), DisplayBound(rangeRule.Max, localizer["ValidationRules:NoMaximum"])],
            RegexRule regexRule => localizer["ValidationRules:Regex", ConfigurationRegexTextCodec.NormalizePattern(regexRule.Pattern)],
            AllowedValuesRule allowedValuesRule => localizer["ValidationRules:AllowedValues", string.Join(", ", allowedValuesRule.Values)],
            MaxLengthRule maxLengthRule => localizer["ValidationRules:MaxLength", maxLengthRule.Max],
            MinLengthRule minLengthRule => localizer["ValidationRules:MinLength", minLengthRule.Min],
            _ => localizer["ValidationRules:Unknown"]
        };
    }

    private static string DisplayBound(decimal? value, string fallback)
    {
        return value?.ToString("G") ?? fallback;
    }
}
