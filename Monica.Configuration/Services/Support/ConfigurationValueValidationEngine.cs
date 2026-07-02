using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Validates JSON configuration values against Monica schema nodes.
/// </summary>
internal sealed class ConfigurationValueValidationEngine
{
    private static readonly string[] TIME_SPAN_FORMATS =
    [
        "c",
        "g",
        "G",
        @"d\.hh\:mm\:ss",
        @"hh\:mm\:ss",
        @"hh\:mm"
    ];

    /// <summary>
    /// Validates a value against a schema node and returns every discovered issue.
    /// </summary>
    /// <param name="schema">The schema node.</param>
    /// <param name="path">The logical path represented by <paramref name="schema"/>.</param>
    /// <param name="value">The JSON value to validate.</param>
    /// <param name="options">Validation options.</param>
    /// <returns>Validation issues.</returns>
    public IReadOnlyList<ConfigurationValueValidationIssue> Validate(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonElement value,
        ConfigurationValueValidationOptions options)
    {
        var issues = new List<ConfigurationValueValidationIssue>();
        ValidateNodeValue(schema, path, value, options, issues);
        return issues;
    }

    /// <summary>
    /// Validates removing a value from a schema node.
    /// </summary>
    /// <param name="schema">The schema node being removed.</param>
    /// <param name="path">The logical path being removed.</param>
    /// <param name="options">Validation options.</param>
    /// <returns>Validation issues.</returns>
    public IReadOnlyList<ConfigurationValueValidationIssue> ValidateRemoval(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        ConfigurationValueValidationOptions options)
    {
        var issues = new List<ConfigurationValueValidationIssue>();
        ValidateMissing(schema, path, options, issues);
        return issues;
    }

    private static void ValidateNodeValue(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonElement value,
        ConfigurationValueValidationOptions options,
        List<ConfigurationValueValidationIssue> issues)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            ValidateNull(schema, path, options, issues);
            return;
        }

        switch (schema.NodeKind)
        {
            case ConfigurationNodeKind.Scalar:
                ValidateScalar(schema, path, value, issues);
                break;
            case ConfigurationNodeKind.Object:
                ValidateObject(schema, path, value, options, issues);
                break;
            case ConfigurationNodeKind.Dictionary:
                ValidateDictionary(schema, path, value, options, issues);
                break;
            case ConfigurationNodeKind.List:
                ValidateList(schema, path, value, options, issues);
                break;
        }
    }

    private static void ValidateObject(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonElement value,
        ConfigurationValueValidationOptions options,
        List<ConfigurationValueValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            AddIssue(schema, path, "Expected a JSON object.", value, issues);
            return;
        }

        foreach (var child in schema.Children)
        {
            var childPath = path.Append(new PropertySegment(child.Name));
            if (!TryGetProperty(value, child.Name, out var childValue))
            {
                ValidateMissing(child, childPath, options, issues);
                continue;
            }

            ValidateNodeValue(child, childPath, childValue, options, issues);
        }
    }

    private static void ValidateDictionary(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonElement value,
        ConfigurationValueValidationOptions options,
        List<ConfigurationValueValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            AddIssue(schema, path, "Expected a JSON object for dictionary configuration.", value, issues);
            return;
        }

        var properties = value.EnumerateObject().ToArray();
        ValidateContainerRules(schema, path, properties.Length, value, issues);
        if (schema.DictionaryTemplate is not { } template)
        {
            return;
        }

        foreach (var property in properties)
        {
            ValidateNodeValue(
                template.ValueTemplate,
                path.Append(new DictionaryKeySegment(property.Name)),
                property.Value,
                options,
                issues);
        }
    }

    private static void ValidateList(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonElement value,
        ConfigurationValueValidationOptions options,
        List<ConfigurationValueValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            AddIssue(schema, path, "Expected a JSON array for list configuration.", value, issues);
            return;
        }

        var items = value.EnumerateArray().ToArray();
        ValidateContainerRules(schema, path, items.Length, value, issues);
        if (schema.ListTemplate is not { } template)
        {
            return;
        }

        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var itemPath = ResolveListItemPath(path, template, index, item);
            ValidateNodeValue(template.ItemTemplate, itemPath, item, options, issues);
        }
    }

    private static void ValidateContainerRules(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        int itemCount,
        JsonElement value,
        List<ConfigurationValueValidationIssue> issues)
    {
        foreach (var rule in schema.ValidationRules)
        {
            switch (rule)
            {
                case RequiredRule when itemCount == 0:
                    AddIssue(schema, path, "value is required and must contain at least 1 item.", value, issues);
                    return;
                case MinLengthRule minLengthRule when itemCount < minLengthRule.Min:
                    AddIssue(schema, path, MinimumItemCountMessage(minLengthRule.Min), value, issues);
                    return;
                case MaxLengthRule maxLengthRule when itemCount > maxLengthRule.Max:
                    AddIssue(schema, path, MaximumItemCountMessage(maxLengthRule.Max), value, issues);
                    return;
            }
        }
    }

    private static void ValidateScalar(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonElement value,
        List<ConfigurationValueValidationIssue> issues)
    {
        string scalar;
        try
        {
            scalar = ConvertScalar(schema, value);
        }
        catch (ConfigurationValueConversionException ex)
        {
            AddIssue(schema, path, ex.Message, value, issues);
            return;
        }

        ValidateScalarRules(schema, path, scalar, value, issues);
    }

    private static string ConvertScalar(
        ConfigurationNodeDefinition schema,
        JsonElement value)
    {
        return schema.ValueKind switch
        {
            ConfigurationValueKind.Boolean => ConvertBoolean(value),
            ConfigurationValueKind.Integer => ConvertInteger(value),
            ConfigurationValueKind.Decimal => ConvertDecimal(value),
            ConfigurationValueKind.Floating => ConvertFloating(value),
            ConfigurationValueKind.DateTime => ConvertDateTime(value),
            ConfigurationValueKind.TimeSpan => ConvertTimeSpan(value),
            ConfigurationValueKind.Enum => ConvertEnum(schema, value),
            ConfigurationValueKind.Uri => ConvertUri(value),
            ConfigurationValueKind.Json => value.GetRawText(),
            _ => ConvertStringLike(value)
        };
    }

    private static string ConvertBoolean(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return value.GetBoolean() ? "true" : "false";
        }

        var text = ReadStringLike(value);
        if (bool.TryParse(text, out var parsed))
        {
            return parsed ? "true" : "false";
        }

        throw ConversionFailed("Expected a boolean value.");
    }

    private static string ConvertInteger(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }

        var text = ReadStringLike(value);
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed.ToString(CultureInfo.InvariantCulture);
        }

        throw ConversionFailed("Expected an integer value.");
    }

    private static string ConvertDecimal(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }

        var text = ReadStringLike(value);
        if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed.ToString(CultureInfo.InvariantCulture);
        }

        throw ConversionFailed("Expected a numeric value.");
    }

    private static string ConvertFloating(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }

        var text = ReadStringLike(value);
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed.ToString(CultureInfo.InvariantCulture);
        }

        throw ConversionFailed("Expected a floating-point value.");
    }

    private static string ConvertDateTime(JsonElement value)
    {
        var text = ReadStringLike(value);
        if (!string.IsNullOrWhiteSpace(text)
            && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return parsed.ToString("O", CultureInfo.InvariantCulture);
        }

        throw ConversionFailed("Expected a date/time value.");
    }

    private static string ConvertTimeSpan(JsonElement value)
    {
        var text = ReadStringLike(value);
        if (!string.IsNullOrWhiteSpace(text)
            && (TimeSpan.TryParseExact(text, TIME_SPAN_FORMATS, CultureInfo.InvariantCulture, out var parsed)
                || TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed)))
        {
            return parsed.ToString("c", CultureInfo.InvariantCulture);
        }

        throw ConversionFailed("Expected a valid time span value.");
    }

    private static string ConvertEnum(
        ConfigurationNodeDefinition schema,
        JsonElement value)
    {
        var text = ConvertStringLike(value);
        return schema.TryNormalizeEnumDisplayValue(text, out var normalized)
            ? normalized
            : text;
    }

    private static string ConvertUri(JsonElement value)
    {
        var text = ConvertStringLike(value);
        if (string.IsNullOrWhiteSpace(text) || Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out _))
        {
            return text;
        }

        throw ConversionFailed("Expected a URI value.");
    }

    private static string ConvertStringLike(JsonElement value)
    {
        return ReadStringLike(value)
               ?? throw ConversionFailed("Expected a scalar value.");
    }

    private static string? ReadStringLike(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static void ValidateScalarRules(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        string value,
        JsonElement sourceValue,
        List<ConfigurationValueValidationIssue> issues)
    {
        var requiredRule = schema.ValidationRules.OfType<RequiredRule>().FirstOrDefault();
        if (requiredRule is not null && string.IsNullOrWhiteSpace(value))
        {
            AddIssue(schema, path, requiredRule.ErrorMessage ?? "A value is required.", sourceValue, issues);
            return;
        }

        foreach (var rule in schema.ValidationRules)
        {
            switch (rule)
            {
                case AllowedValuesRule allowedValuesRule when !string.IsNullOrWhiteSpace(value)
                                                              && !allowedValuesRule.Values.Contains(value, StringComparer.OrdinalIgnoreCase):
                    AddIssue(schema, path, rule.ErrorMessage ?? $"Value must be one of: {string.Join(", ", allowedValuesRule.Values)}.", sourceValue, issues);
                    return;
                case RegexRule regexRule when !string.IsNullOrWhiteSpace(value) && !Regex.IsMatch(value, regexRule.Pattern):
                    AddIssue(schema, path, rule.ErrorMessage ?? "Value does not match the required pattern.", sourceValue, issues);
                    return;
                case RangeRule rangeRule when decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue):
                    if (rangeRule.Min is not null && decimalValue < rangeRule.Min || rangeRule.Max is not null && decimalValue > rangeRule.Max)
                    {
                        AddIssue(schema, path, rule.ErrorMessage ?? "Value is outside the allowed range.", sourceValue, issues);
                        return;
                    }

                    break;
                case MaxLengthRule maxLengthRule when value.Length > maxLengthRule.Max:
                    AddIssue(schema, path, rule.ErrorMessage ?? $"Value must be at most {maxLengthRule.Max} characters.", sourceValue, issues);
                    return;
                case MinLengthRule minLengthRule when value.Length < minLengthRule.Min:
                    AddIssue(schema, path, rule.ErrorMessage ?? $"Value must be at least {minLengthRule.Min} characters.", sourceValue, issues);
                    return;
            }
        }
    }

    private static void ValidateNull(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        ConfigurationValueValidationOptions options,
        List<ConfigurationValueValidationIssue> issues)
    {
        if (BuildMissingProblem(schema, options) is { } problem)
        {
            AddMissingIssue(schema, path, problem, issues);
        }
    }

    private static void ValidateMissing(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        ConfigurationValueValidationOptions options,
        List<ConfigurationValueValidationIssue> issues)
    {
        if (BuildMissingProblem(schema, options) is { } problem)
        {
            AddMissingIssue(schema, path, problem, issues);
        }
    }

    private static string? BuildMissingProblem(
        ConfigurationNodeDefinition schema,
        ConfigurationValueValidationOptions options)
    {
        var minLengthRule = schema.ValidationRules.OfType<MinLengthRule>().FirstOrDefault();
        if (schema.NodeKind is ConfigurationNodeKind.Dictionary or ConfigurationNodeKind.List
            && minLengthRule is not null
            && minLengthRule.Min > 0)
        {
            return MinimumItemCountMessage(minLengthRule.Min);
        }

        if (schema.ValidationRules.OfType<RequiredRule>().Any())
        {
            return "A value is required.";
        }

        if (schema.NodeKind == ConfigurationNodeKind.Scalar
            && options.TreatNonNullableScalarsAsRequired
            && !schema.IsNullable)
        {
            return "A value is required.";
        }

        return null;
    }

    private static string MinimumItemCountMessage(int minimum)
    {
        return minimum == 1
            ? "value is required and must contain at least 1 item."
            : $"value is required and must contain at least {minimum} items.";
    }

    private static string MaximumItemCountMessage(int maximum)
    {
        return maximum == 1
            ? "value must contain at most 1 item."
            : $"value must contain at most {maximum} items.";
    }

    private static LogicalPath ResolveListItemPath(
        LogicalPath listPath,
        ConfigurationListTemplate template,
        int index,
        JsonElement item)
    {
        if (template.SupportsPerItemMutation
            && TryReadObjectScalar(item, template.ItemKeyPropertyName!) is { Length: > 0 } itemKey)
        {
            return listPath.Append(new ListItemKeySegment(itemKey));
        }

        return listPath.Append(new ListIndexSegment(index));
    }

    private static string? TryReadObjectScalar(JsonElement item, string propertyName)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in item.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return ReadStringLike(property.Value);
            }
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement value, string propertyName, out JsonElement propertyValue)
    {
        foreach (var property in value.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                propertyValue = property.Value;
                return true;
            }
        }

        propertyValue = default;
        return false;
    }

    private static void AddMissingIssue(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        string message,
        List<ConfigurationValueValidationIssue> issues)
    {
        issues.Add(new ConfigurationValueValidationIssue
        {
            LogicalPath = path,
            Node = schema,
            Message = message,
            IsMissing = true,
            ValidationRules = schema.ValidationRules
        });
    }

    private static void AddIssue(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        string message,
        JsonElement value,
        List<ConfigurationValueValidationIssue> issues)
    {
        issues.Add(new ConfigurationValueValidationIssue
        {
            LogicalPath = path,
            Node = schema,
            Message = message,
            DisplayValue = DisplayValue(value),
            ValidationRules = schema.ValidationRules
        });
    }

    private static string DisplayValue(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.GetRawText();
    }

    private static ConfigurationValueConversionException ConversionFailed(string message)
    {
        return new ConfigurationValueConversionException(message);
    }

    private sealed class ConfigurationValueConversionException(string message) : Exception(message);
}

/// <summary>
/// Controls schema validation behavior for a specific caller.
/// </summary>
internal sealed record ConfigurationValueValidationOptions
{
    /// <summary>
    /// Gets whether non-nullable scalar nodes should be treated as required when no explicit rule exists.
    /// </summary>
    public bool TreatNonNullableScalarsAsRequired { get; init; }
}

/// <summary>
/// Describes one raw schema validation issue before runtime source metadata is attached.
/// </summary>
internal sealed record ConfigurationValueValidationIssue
{
    /// <summary>
    /// Gets the target logical path.
    /// </summary>
    public required LogicalPath LogicalPath { get; init; }

    /// <summary>
    /// Gets the schema node that failed validation.
    /// </summary>
    public required ConfigurationNodeDefinition Node { get; init; }

    /// <summary>
    /// Gets the validation message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the display-safe invalid value when one exists.
    /// </summary>
    public string? DisplayValue { get; init; }

    /// <summary>
    /// Gets whether the value is missing.
    /// </summary>
    public bool IsMissing { get; init; }

    /// <summary>
    /// Gets the rules that constrained the value.
    /// </summary>
    public IReadOnlyList<ConfigurationValidationRule> ValidationRules { get; init; } = [];
}
