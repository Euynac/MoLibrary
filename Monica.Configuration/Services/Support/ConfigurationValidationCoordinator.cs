using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Monica.Configuration.Exceptions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Coordinates mutation-time validation.
/// </summary>
internal sealed class ConfigurationValidationCoordinator
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
    /// Validates a mutation against the current Phase 1 schema constraints.
    /// </summary>
    /// <param name="definition">The target definition.</param>
    /// <param name="request">The mutation request.</param>
    public void Validate(ConfigurationDefinition definition, ConfigurationMutationRequest request)
    {
        if (request.ExpectedSchemaVersion > 0 && request.ExpectedSchemaVersion != definition.SchemaVersion)
        {
            throw new Exceptions.ConfigurationSchemaMismatchException(
                $"Expected schema version {request.ExpectedSchemaVersion}, but definition '{definition.DefinitionKey}' is version {definition.SchemaVersion}.");
        }

        var target = ResolveTargetNode(definition, request.LogicalPath);
        if (request.MutationKind == ConfigurationMutationKind.Remove)
        {
            ValidateRemove(target, request.LogicalPath);
            return;
        }

        using var document = JsonDocument.Parse(request.Value.Json);
        ValidateNodeValue(target, request.LogicalPath, document.RootElement);
    }

    private static void ValidateRemove(ConfigurationNodeDefinition target, LogicalPath logicalPath)
    {
        if (target.NodeKind == ConfigurationNodeKind.Scalar
            && IsRequired(target))
        {
            throw ValidationFailed(logicalPath, "Required configuration values cannot be removed.");
        }
    }

    private static void ValidateNodeValue(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            ValidateNull(schema, path);
            return;
        }

        switch (schema.NodeKind)
        {
            case ConfigurationNodeKind.Scalar:
                ValidateScalar(schema, path, value);
                break;
            case ConfigurationNodeKind.Object:
                ValidateObject(schema, path, value);
                break;
            case ConfigurationNodeKind.Dictionary:
                ValidateDictionary(schema, path, value);
                break;
            case ConfigurationNodeKind.List:
                ValidateList(schema, path, value);
                break;
        }
    }

    private static void ValidateObject(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw ValidationFailed(path, "Expected a JSON object.");
        }

        foreach (var child in schema.Children)
        {
            var childPath = path.Append(new PropertySegment(child.Name));
            if (!TryGetProperty(value, child.Name, out var childValue))
            {
                ValidateMissing(child, childPath);
                continue;
            }

            ValidateNodeValue(child, childPath, childValue);
        }
    }

    private static void ValidateDictionary(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw ValidationFailed(path, "Expected a JSON object for dictionary configuration.");
        }

        if (schema.DictionaryTemplate is not { } template)
        {
            return;
        }

        foreach (var property in value.EnumerateObject())
        {
            ValidateNodeValue(
                template.ValueTemplate,
                path.Append(new DictionaryKeySegment(property.Name)),
                property.Value);
        }
    }

    private static void ValidateList(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw ValidationFailed(path, "Expected a JSON array for list configuration.");
        }

        if (schema.ListTemplate is not { } template)
        {
            return;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemPath = ResolveListItemPath(path, template, index, item);
            ValidateNodeValue(template.ItemTemplate, itemPath, item);
            index++;
        }
    }

    private static void ValidateScalar(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonElement value)
    {
        var scalar = ConvertScalar(schema, path, value);
        ValidateRules(schema, path, scalar);
    }

    private static string ConvertScalar(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonElement value)
    {
        return schema.ValueKind switch
        {
            ConfigurationValueKind.Boolean => ConvertBoolean(path, value),
            ConfigurationValueKind.Integer => ConvertInteger(path, value),
            ConfigurationValueKind.Decimal => ConvertDecimal(path, value),
            ConfigurationValueKind.Floating => ConvertFloating(path, value),
            ConfigurationValueKind.DateTime => ConvertDateTime(path, value),
            ConfigurationValueKind.TimeSpan => ConvertTimeSpan(path, value),
            ConfigurationValueKind.Enum => ConvertEnum(schema, path, value),
            ConfigurationValueKind.Uri => ConvertUri(path, value),
            ConfigurationValueKind.Json => value.GetRawText(),
            _ => ConvertStringLike(path, value)
        };
    }

    private static string ConvertBoolean(LogicalPath path, JsonElement value)
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

        throw ValidationFailed(path, "Expected a boolean value.");
    }

    private static string ConvertInteger(LogicalPath path, JsonElement value)
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

        throw ValidationFailed(path, "Expected an integer value.");
    }

    private static string ConvertDecimal(LogicalPath path, JsonElement value)
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

        throw ValidationFailed(path, "Expected a numeric value.");
    }

    private static string ConvertFloating(LogicalPath path, JsonElement value)
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

        throw ValidationFailed(path, "Expected a floating-point value.");
    }

    private static string ConvertDateTime(LogicalPath path, JsonElement value)
    {
        var text = ReadStringLike(value);
        if (!string.IsNullOrWhiteSpace(text)
            && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            return parsed.ToString("O", CultureInfo.InvariantCulture);
        }

        throw ValidationFailed(path, "Expected a date/time value.");
    }

    private static string ConvertTimeSpan(LogicalPath path, JsonElement value)
    {
        var text = ReadStringLike(value);
        if (!string.IsNullOrWhiteSpace(text)
            && (TimeSpan.TryParseExact(text, TIME_SPAN_FORMATS, CultureInfo.InvariantCulture, out var parsed)
                || TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed)))
        {
            return parsed.ToString("c", CultureInfo.InvariantCulture);
        }

        throw ValidationFailed(path, "Expected a valid time span value.");
    }

    private static string ConvertEnum(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        JsonElement value)
    {
        var text = ConvertStringLike(path, value);
        return schema.TryNormalizeEnumDisplayValue(text, out var normalized)
            ? normalized
            : text;
    }

    private static string ConvertUri(LogicalPath path, JsonElement value)
    {
        var text = ConvertStringLike(path, value);
        if (string.IsNullOrWhiteSpace(text) || Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out _))
        {
            return text;
        }

        throw ValidationFailed(path, "Expected a URI value.");
    }

    private static string ConvertStringLike(LogicalPath path, JsonElement value)
    {
        return ReadStringLike(value)
               ?? throw ValidationFailed(path, "Expected a scalar value.");
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

    private static void ValidateRules(
        ConfigurationNodeDefinition schema,
        LogicalPath path,
        string value)
    {
        if (IsRequired(schema) && string.IsNullOrWhiteSpace(value))
        {
            throw ValidationFailed(path, "A value is required.");
        }

        foreach (var rule in schema.ValidationRules)
        {
            switch (rule)
            {
                case AllowedValuesRule allowedValuesRule when !string.IsNullOrWhiteSpace(value)
                                                              && !allowedValuesRule.Values.Contains(value, StringComparer.OrdinalIgnoreCase):
                    throw ValidationFailed(path, rule.ErrorMessage ?? $"Value must be one of: {string.Join(", ", allowedValuesRule.Values)}.");
                case RegexRule regexRule when !string.IsNullOrWhiteSpace(value) && !Regex.IsMatch(value, regexRule.Pattern):
                    throw ValidationFailed(path, rule.ErrorMessage ?? "Value does not match the required pattern.");
                case RangeRule rangeRule when decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue):
                    if (rangeRule.Min is not null && decimalValue < rangeRule.Min || rangeRule.Max is not null && decimalValue > rangeRule.Max)
                    {
                        throw ValidationFailed(path, rule.ErrorMessage ?? "Value is outside the allowed range.");
                    }

                    break;
                case MaxLengthRule maxLengthRule when value.Length > maxLengthRule.Max:
                    throw ValidationFailed(path, rule.ErrorMessage ?? $"Value must be at most {maxLengthRule.Max} characters.");
                case MinLengthRule minLengthRule when value.Length < minLengthRule.Min:
                    throw ValidationFailed(path, rule.ErrorMessage ?? $"Value must be at least {minLengthRule.Min} characters.");
            }
        }
    }

    private static void ValidateNull(ConfigurationNodeDefinition schema, LogicalPath path)
    {
        if (!schema.IsNullable || IsRequired(schema))
        {
            throw ValidationFailed(path, "A value is required.");
        }
    }

    private static void ValidateMissing(ConfigurationNodeDefinition schema, LogicalPath path)
    {
        if (schema.NodeKind == ConfigurationNodeKind.Scalar && (!schema.IsNullable || IsRequired(schema)))
        {
            throw ValidationFailed(path, "A value is required.");
        }
    }

    private static bool IsRequired(ConfigurationNodeDefinition schema)
    {
        return schema.ValidationRules.OfType<RequiredRule>().Any();
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

    private static ConfigurationNodeDefinition ResolveTargetNode(
        ConfigurationDefinition definition,
        LogicalPath logicalPath)
    {
        var current = definition.Root;
        foreach (var segment in logicalPath.Segments)
        {
            current = segment switch
            {
                PropertySegment property => current.Children.FirstOrDefault(child =>
                    string.Equals(child.Name, property.Name, StringComparison.OrdinalIgnoreCase)),
                DictionaryKeySegment => current.DictionaryTemplate?.ValueTemplate,
                ListItemKeySegment or ListIndexSegment => current.ListTemplate?.ItemTemplate,
                _ => null
            };

            if (current is null)
            {
                throw ValidationFailed(logicalPath, $"Logical path '{logicalPath}' does not exist in definition '{definition.DefinitionKey}'.");
            }
        }

        return current;
    }

    private static ConfigurationValidationFailedException ValidationFailed(
        LogicalPath path,
        string message)
    {
        return new ConfigurationValidationFailedException($"{path.ToCanonicalString()}: {message}");
    }
}
