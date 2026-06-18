using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;
using Monica.Configuration.Models;
using Monica.Configuration.UI.Localization;

namespace Monica.Configuration.UI.Support;

internal static class ConfigurationScalarValueCodec
{
    public static ConfigurationScalarValueResult ConvertDisplayValue(
        ConfigurationNodeDefinition node,
        string? displayValue,
        IStringLocalizer<ConfigurationUIResource> localizer,
        string? rawJson = null)
    {
        displayValue ??= string.Empty;

        var normalizedDisplayValue = NormalizeDisplayValue(node, displayValue);
        var validationError = ValidateDisplayValue(node, normalizedDisplayValue, localizer);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return ConfigurationScalarValueResult.Invalid(displayValue, validationError);
        }

        var storedJson = rawJson ?? CreateDefaultStoredJson(node, normalizedDisplayValue);
        return ConfigurationScalarValueResult.Valid(
            ConfigurationStoredValue.FromJson(storedJson),
            normalizedDisplayValue);
    }

    public static string? ValidateDisplayValue(
        ConfigurationNodeDefinition node,
        string? value,
        IStringLocalizer<ConfigurationUIResource> localizer)
    {
        value ??= string.Empty;
        value = NormalizeDisplayValue(node, value);

        if (string.IsNullOrWhiteSpace(value))
        {
            return RequiresValue(node)
                ? localizer["State:Editor:Required"].Value
                : null;
        }

        var kindError = ValidateValueKind(node, value, localizer);
        if (!string.IsNullOrWhiteSpace(kindError))
        {
            return kindError;
        }

        foreach (var rule in node.ValidationRules)
        {
            switch (rule)
            {
                case AllowedValuesRule allowedValuesRule when !allowedValuesRule.Values.Contains(value, StringComparer.OrdinalIgnoreCase):
                    return rule.ErrorMessage ?? localizer["State:Editor:InvalidPattern"];
                case RegexRule regexRule when !Regex.IsMatch(value, regexRule.Pattern):
                    return rule.ErrorMessage ?? localizer["State:Editor:InvalidPattern"];
                case RangeRule rangeRule when decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue):
                    if (rangeRule.Min is not null && decimalValue < rangeRule.Min || rangeRule.Max is not null && decimalValue > rangeRule.Max)
                    {
                        return rule.ErrorMessage ?? localizer["State:Editor:OutOfRange"];
                    }

                    break;
                case MaxLengthRule maxLengthRule when value.Length > maxLengthRule.Max:
                    return rule.ErrorMessage ?? localizer["State:Editor:TooLong"];
                case MinLengthRule minLengthRule when value.Length < minLengthRule.Min:
                    return rule.ErrorMessage ?? localizer["State:Editor:TooShort"];
            }
        }

        return null;
    }

    public static IReadOnlyList<string> GetAllowedValues(ConfigurationNodeDefinition node)
    {
        return node.ValidationRules.OfType<AllowedValuesRule>().FirstOrDefault()?.Values
               ?? ExtractRegexEnumValues(node);
    }

    public static string NormalizeDisplayValue(ConfigurationNodeDefinition node, string displayValue)
    {
        if (node.ValueKind == ConfigurationValueKind.Enum
            && node.TryNormalizeEnumDisplayValue(displayValue, out var enumDisplayValue))
        {
            return enumDisplayValue;
        }

        return node.ValueKind == ConfigurationValueKind.TimeSpan && ConfigurationScalarTextCodec.TryParseTimeSpan(displayValue, out var value)
            ? ConfigurationScalarTextCodec.FormatTimeSpan(value)
            : displayValue;
    }

    public static string CreateDefaultStoredJson(ConfigurationNodeDefinition node, string displayValue)
    {
        return node.ValueKind switch
        {
            ConfigurationValueKind.Boolean when bool.TryParse(displayValue, out var boolValue) =>
                boolValue ? "true" : "false",
            ConfigurationValueKind.Integer when long.TryParse(displayValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integerValue) =>
                integerValue.ToString(CultureInfo.InvariantCulture),
            ConfigurationValueKind.Decimal or ConfigurationValueKind.Floating
                when decimal.TryParse(displayValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue) =>
                decimalValue.ToString(CultureInfo.InvariantCulture),
            ConfigurationValueKind.DateTime when DateTime.TryParse(displayValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dateTimeValue) =>
                JsonSerializer.Serialize(dateTimeValue.ToString("O", CultureInfo.InvariantCulture)),
            ConfigurationValueKind.TimeSpan when ConfigurationScalarTextCodec.TryParseTimeSpan(displayValue, out var timeSpanValue) =>
                ConfigurationScalarTextCodec.ToTimeSpanJson(timeSpanValue),
            ConfigurationValueKind.Enum =>
                JsonSerializer.Serialize(displayValue),
            ConfigurationValueKind.Json when !string.IsNullOrWhiteSpace(displayValue) =>
                displayValue,
            _ => JsonSerializer.Serialize(displayValue)
        };
    }

    private static string? ValidateValueKind(
        ConfigurationNodeDefinition node,
        string value,
        IStringLocalizer<ConfigurationUIResource> localizer)
    {
        return node.ValueKind switch
        {
            ConfigurationValueKind.TimeSpan when !ConfigurationScalarTextCodec.TryParseTimeSpan(value, out _) =>
                localizer["State:Editor:InvalidTimeSpan"].Value,
            ConfigurationValueKind.DateTime when !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out _) =>
                localizer["ImportExport:Diagnostics:ExpectedDateTime"].Value,
            ConfigurationValueKind.Integer when !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) =>
                localizer["ImportExport:Diagnostics:ExpectedInteger"].Value,
            ConfigurationValueKind.Decimal or ConfigurationValueKind.Floating
                when !decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _) =>
                localizer["ImportExport:Diagnostics:ExpectedNumber"].Value,
            _ => null
        };
    }

    private static bool RequiresValue(ConfigurationNodeDefinition node)
    {
        return node.ValidationRules.OfType<RequiredRule>().Any()
               || !node.IsNullable
               && node.ValueKind is ConfigurationValueKind.TimeSpan
                   or ConfigurationValueKind.DateTime
                   or ConfigurationValueKind.Integer
                   or ConfigurationValueKind.Decimal
                   or ConfigurationValueKind.Floating;
    }

    private static IReadOnlyList<string> ExtractRegexEnumValues(ConfigurationNodeDefinition node)
    {
        var pattern = node.ValidationRules.OfType<RegexRule>().FirstOrDefault()?.Pattern;
        if (string.IsNullOrWhiteSpace(pattern) || !pattern.StartsWith("^(", StringComparison.Ordinal) || !pattern.EndsWith(")$", StringComparison.Ordinal))
        {
            return [];
        }

        return pattern[2..^2].Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

internal sealed record ConfigurationScalarValueResult
{
    public required bool IsValid { get; init; }

    public ConfigurationStoredValue? StoredValue { get; init; }

    public required string DisplayValue { get; init; }

    public string? ValidationError { get; init; }

    public static ConfigurationScalarValueResult Valid(ConfigurationStoredValue storedValue, string displayValue)
    {
        return new ConfigurationScalarValueResult
        {
            IsValid = true,
            StoredValue = storedValue,
            DisplayValue = displayValue
        };
    }

    public static ConfigurationScalarValueResult Invalid(string displayValue, string validationError)
    {
        return new ConfigurationScalarValueResult
        {
            IsValid = false,
            DisplayValue = displayValue,
            ValidationError = validationError
        };
    }
}
