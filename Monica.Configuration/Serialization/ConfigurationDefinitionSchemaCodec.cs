using System.Text.Json;
using Monica.Configuration.Models;
using Monica.Tool.Extensions;

namespace Monica.Configuration.Serialization;

/// <summary>
/// Serializes and restores the portable portion of a configuration definition schema.
/// </summary>
public static class ConfigurationDefinitionSchemaCodec
{
    /// <summary>
    /// Serializes the definition schema without runtime-only path or CLR assembly metadata.
    /// </summary>
    /// <param name="definition">The definition to serialize.</param>
    /// <returns>The compact schema JSON.</returns>
    public static string SerializeSchema(ConfigurationDefinition definition)
    {
        return JsonSerializer.Serialize(ToNodeDto(definition.Root), ConfigurationPersistedJsonOptions.CompactSchema);
    }

    /// <summary>
    /// Serializes the stable hash input for a definition.
    /// </summary>
    /// <param name="definitionKey">The definition key.</param>
    /// <param name="sectionPath">The binding section path.</param>
    /// <param name="root">The root schema node.</param>
    /// <returns>Compact JSON suitable for deterministic hashing.</returns>
    public static string SerializeHashInput(string definitionKey, string sectionPath, ConfigurationNodeDefinition root)
    {
        var input = new SchemaHashInputDto
        {
            DefinitionKey = definitionKey,
            SectionPath = sectionPath,
            Root = ToNodeDto(root)
        };
        return JsonSerializer.Serialize(input, ConfigurationPersistedJsonOptions.CompactSchema);
    }

    /// <summary>
    /// Rebuilds a runtime definition from compact persisted schema metadata.
    /// </summary>
    /// <param name="definitionKey">The stable published definition key.</param>
    /// <param name="sectionPath">The Microsoft configuration section path used by the owner.</param>
    /// <param name="displayName">The operator-facing definition display name.</param>
    /// <param name="description">The optional operator-facing definition description.</param>
    /// <param name="clrTypeName">The compact root type identity stored for display and search.</param>
    /// <param name="fromProject">The assembly name of the project that published the definition.</param>
    /// <param name="category">The optional operator-facing category.</param>
    /// <param name="schemaVersion">The published schema version.</param>
    /// <param name="schemaHash">The published schema hash used for drift detection.</param>
    /// <param name="reloadBehavior">The definition-level reload behavior.</param>
    /// <param name="schemaJson">The compact schema JSON.</param>
    /// <param name="origin">The origin marker to attach to the rebuilt definition.</param>
    /// <returns>A runtime definition with reconstructed logical and Microsoft configuration paths.</returns>
    public static ConfigurationDefinition DeserializeDefinition(
        string definitionKey,
        string sectionPath,
        string displayName,
        string? description,
        string clrTypeName,
        string fromProject,
        string? category,
        int schemaVersion,
        string schemaHash,
        ConfigurationReloadBehavior reloadBehavior,
        string schemaJson,
        ConfigurationDefinitionOrigin origin)
    {
        var rootDto = JsonSerializer.Deserialize<NodeDto>(schemaJson, ConfigurationPersistedJsonOptions.CompactSchema)
            ?? throw new InvalidOperationException($"Configuration definition '{definitionKey}' has empty schema JSON.");

        return new ConfigurationDefinition
        {
            DefinitionKey = definitionKey,
            SectionPath = sectionPath,
            DisplayName = displayName,
            Description = description,
            ClrTypeName = clrTypeName,
            FromProject = fromProject,
            Category = category,
            SchemaVersion = schemaVersion,
            SchemaHash = schemaHash,
            ReloadBehavior = reloadBehavior,
            Root = FromNodeDto(rootDto, LogicalPath.Root, sectionPath, clrTypeName),
            Origin = origin
        };
    }

    /// <summary>
    /// Converts an assembly-qualified CLR type name into a compact display/search identity.
    /// </summary>
    /// <param name="clrTypeName">The CLR type name captured from scanner metadata.</param>
    /// <returns>A compact type name without assembly identity when it can be resolved.</returns>
    public static string ToCompactClrTypeName(string clrTypeName)
    {
        if (string.IsNullOrWhiteSpace(clrTypeName))
        {
            return string.Empty;
        }

        var type = Type.GetType(clrTypeName, throwOnError: false);
        if (type is not null)
        {
            return type.GetCleanFullName();
        }

        var assemblySeparator = clrTypeName.IndexOf(',', StringComparison.Ordinal);
        return assemblySeparator >= 0 ? clrTypeName[..assemblySeparator].Trim() : clrTypeName.Trim();
    }

    private static NodeDto ToNodeDto(ConfigurationNodeDefinition node)
    {
        var canonicalPath = node.RelativePath.ToCanonicalString();
        return new NodeDto
        {
            NodeKey = string.Equals(node.NodeKey, canonicalPath, StringComparison.Ordinal) ? null : node.NodeKey,
            Name = node.Name,
            DisplayName = NullIfWhiteSpace(node.DisplayName),
            Description = NullIfWhiteSpace(node.Description),
            NodeKind = node.NodeKind,
            ValueKind = node.NodeKind == ConfigurationNodeKind.Scalar ? node.ValueKind : null,
            IsNullable = node.IsNullable,
            IsSensitive = node.IsSensitive,
            ReloadBehavior = node.ReloadBehavior,
            DictionaryTemplate = node.DictionaryTemplate is null ? null : ToDictionaryDto(node.DictionaryTemplate),
            ListTemplate = node.ListTemplate is null ? null : ToListDto(node.ListTemplate),
            Children = node.Children.Count == 0 ? null : node.Children.Select(ToNodeDto).ToArray(),
            ValidationRules = node.ValidationRules.Count == 0 ? null : node.ValidationRules.Select(ToRuleDto).ToArray(),
            EnumValues = node.EnumValues.Count == 0 ? null : node.EnumValues.Select(ToEnumValueDto).ToArray()
        };
    }

    private static EnumValueDto ToEnumValueDto(ConfigurationEnumValue value)
    {
        return new EnumValueDto
        {
            Name = value.Name,
            Value = value.Value
        };
    }

    private static DictionaryTemplateDto ToDictionaryDto(ConfigurationDictionaryTemplate template)
    {
        return new DictionaryTemplateDto
        {
            KeyKind = template.KeyKind,
            KeyRegexPattern = NullIfWhiteSpace(template.KeyRegexPattern),
            DisallowColonInKey = template.DisallowColonInKey ? null : false,
            ValueTemplate = ToNodeDto(template.ValueTemplate)
        };
    }

    private static ListTemplateDto ToListDto(ConfigurationListTemplate template)
    {
        return new ListTemplateDto
        {
            AllowDuplicateItems = template.AllowDuplicateItems,
            ItemKeyPropertyName = NullIfWhiteSpace(template.ItemKeyPropertyName),
            ItemTemplate = ToNodeDto(template.ItemTemplate)
        };
    }

    private static RuleDto ToRuleDto(ConfigurationValidationRule rule)
    {
        return rule switch
        {
            RequiredRule required => new RuleDto
            {
                Kind = RuleKind.Required,
                ErrorMessage = NullIfWhiteSpace(required.ErrorMessage)
            },
            RangeRule range => new RuleDto
            {
                Kind = RuleKind.Range,
                ErrorMessage = NullIfWhiteSpace(range.ErrorMessage),
                Min = range.Min,
                Max = range.Max
            },
            RegexRule regex => new RuleDto
            {
                Kind = RuleKind.Regex,
                ErrorMessage = NullIfWhiteSpace(regex.ErrorMessage),
                Pattern = regex.Pattern
            },
            AllowedValuesRule allowed => new RuleDto
            {
                Kind = RuleKind.AllowedValues,
                ErrorMessage = NullIfWhiteSpace(allowed.ErrorMessage),
                Values = allowed.Values.Count == 0 ? null : allowed.Values
            },
            MaxLengthRule maxLength => new RuleDto
            {
                Kind = RuleKind.MaxLength,
                ErrorMessage = NullIfWhiteSpace(maxLength.ErrorMessage),
                Length = maxLength.Max
            },
            MinLengthRule minLength => new RuleDto
            {
                Kind = RuleKind.MinLength,
                ErrorMessage = NullIfWhiteSpace(minLength.ErrorMessage),
                Length = minLength.Min
            },
            _ => throw new InvalidOperationException($"Unsupported configuration validation rule '{rule.GetType().Name}'.")
        };
    }

    private static ConfigurationNodeDefinition FromNodeDto(
        NodeDto dto,
        LogicalPath path,
        string sectionPath,
        string rootClrTypeName)
    {
        var nodeKind = dto.NodeKind;
        var configurationPath = ProjectConfigurationPath(sectionPath, path);
        var children = (dto.Children ?? [])
            .Select(child => FromNodeDto(
                child,
                path.Append(new PropertySegment(child.Name)),
                sectionPath,
                rootClrTypeName))
            .ToArray();

        return new ConfigurationNodeDefinition
        {
            NodeKey = string.IsNullOrWhiteSpace(dto.NodeKey) ? path.ToCanonicalString() : dto.NodeKey!,
            Name = dto.Name,
            DisplayName = dto.DisplayName,
            Description = dto.Description,
            RelativePath = path,
            ConfigurationPath = configurationPath,
            ClrTypeName = ResolveRuntimeClrTypeName(dto, path, rootClrTypeName),
            NodeKind = nodeKind,
            ValueKind = nodeKind == ConfigurationNodeKind.Scalar ? dto.ValueKind ?? ConfigurationValueKind.String : null,
            IsNullable = dto.IsNullable,
            IsSensitive = dto.IsSensitive,
            ReloadBehavior = dto.ReloadBehavior,
            DictionaryTemplate = dto.DictionaryTemplate is null
                ? null
                : FromDictionaryDto(dto.DictionaryTemplate, path, sectionPath, rootClrTypeName),
            ListTemplate = dto.ListTemplate is null
                ? null
                : FromListDto(dto.ListTemplate, path, sectionPath, rootClrTypeName),
            Children = children,
            ValidationRules = (dto.ValidationRules ?? []).Select(FromRuleDto).ToArray(),
            EnumValues = (dto.EnumValues ?? []).Select(FromEnumValueDto).ToArray()
        };
    }

    private static ConfigurationEnumValue FromEnumValueDto(EnumValueDto dto)
    {
        return new ConfigurationEnumValue
        {
            Name = dto.Name,
            Value = dto.Value
        };
    }

    private static ConfigurationDictionaryTemplate FromDictionaryDto(
        DictionaryTemplateDto dto,
        LogicalPath dictionaryPath,
        string sectionPath,
        string rootClrTypeName)
    {
        var keyKind = dto.KeyKind;
        return new ConfigurationDictionaryTemplate
        {
            KeyClrTypeName = ClrTypeNameForValueKind(keyKind),
            KeyKind = keyKind,
            KeyRegexPattern = dto.KeyRegexPattern,
            ValueTemplate = FromNodeDto(
                dto.ValueTemplate,
                dictionaryPath.Append(new DictionaryKeySegment("*")),
                sectionPath,
                rootClrTypeName),
            DisallowColonInKey = dto.DisallowColonInKey ?? true
        };
    }

    private static ConfigurationListTemplate FromListDto(
        ListTemplateDto dto,
        LogicalPath listPath,
        string sectionPath,
        string rootClrTypeName)
    {
        return new ConfigurationListTemplate
        {
            AllowDuplicateItems = dto.AllowDuplicateItems,
            ItemKeyPropertyName = dto.ItemKeyPropertyName,
            ItemTemplate = FromNodeDto(
                dto.ItemTemplate,
                listPath.Append(new ListItemKeySegment("*")),
                sectionPath,
                rootClrTypeName)
        };
    }

    private static ConfigurationValidationRule FromRuleDto(RuleDto dto)
    {
        return (dto.Kind ?? RuleKind.Required) switch
        {
            RuleKind.Required => new RequiredRule { ErrorMessage = dto.ErrorMessage },
            RuleKind.Range => new RangeRule(dto.Min, dto.Max) { ErrorMessage = dto.ErrorMessage },
            RuleKind.Regex => new RegexRule(dto.Pattern ?? "") { ErrorMessage = dto.ErrorMessage },
            RuleKind.AllowedValues => new AllowedValuesRule(dto.Values ?? []) { ErrorMessage = dto.ErrorMessage },
            RuleKind.MaxLength => new MaxLengthRule(dto.Length) { ErrorMessage = dto.ErrorMessage },
            RuleKind.MinLength => new MinLengthRule(dto.Length) { ErrorMessage = dto.ErrorMessage },
            _ => throw new InvalidOperationException($"Unsupported persisted validation rule kind '{dto.Kind}'.")
        };
    }

    private static string ResolveRuntimeClrTypeName(NodeDto dto, LogicalPath path, string rootClrTypeName)
    {
        if (path.Depth == 0 && !string.IsNullOrWhiteSpace(rootClrTypeName))
        {
            return rootClrTypeName;
        }

        return dto.NodeKind switch
        {
            ConfigurationNodeKind.Scalar => ClrTypeNameForValueKind(dto.ValueKind ?? ConfigurationValueKind.String),
            ConfigurationNodeKind.Dictionary => typeof(Dictionary<string, object>).FullName!,
            ConfigurationNodeKind.List => typeof(List<object>).FullName!,
            _ => typeof(object).FullName!
        };
    }

    private static string ClrTypeNameForValueKind(ConfigurationValueKind kind)
    {
        return kind switch
        {
            ConfigurationValueKind.Boolean => typeof(bool).FullName!,
            ConfigurationValueKind.Integer => typeof(long).FullName!,
            ConfigurationValueKind.Decimal => typeof(decimal).FullName!,
            ConfigurationValueKind.Floating => typeof(double).FullName!,
            ConfigurationValueKind.DateTime => typeof(DateTimeOffset).FullName!,
            ConfigurationValueKind.TimeSpan => typeof(TimeSpan).FullName!,
            ConfigurationValueKind.Uri => typeof(Uri).FullName!,
            ConfigurationValueKind.Json => typeof(object).FullName!,
            _ => typeof(string).FullName!
        };
    }

    private static string ProjectConfigurationPath(string sectionPath, LogicalPath path)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(sectionPath))
        {
            parts.Add(sectionPath);
        }

        parts.AddRange(path.Segments.Select(segment => segment.Value));
        return string.Join(':', parts);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed record SchemaHashInputDto
    {
        public required string DefinitionKey { get; init; }

        public required string SectionPath { get; init; }

        public required NodeDto Root { get; init; }
    }

    private sealed record NodeDto
    {
        public string? NodeKey { get; init; }

        public string Name { get; init; } = "";

        public string? DisplayName { get; init; }

        public string? Description { get; init; }

        public ConfigurationNodeKind NodeKind { get; init; }

        public ConfigurationValueKind? ValueKind { get; init; }

        public bool IsNullable { get; init; }

        public bool IsSensitive { get; init; }

        public ConfigurationReloadBehavior? ReloadBehavior { get; init; }

        public DictionaryTemplateDto? DictionaryTemplate { get; init; }

        public ListTemplateDto? ListTemplate { get; init; }

        public IReadOnlyList<NodeDto>? Children { get; init; }

        public IReadOnlyList<RuleDto>? ValidationRules { get; init; }

        public IReadOnlyList<EnumValueDto>? EnumValues { get; init; }
    }

    private sealed record EnumValueDto
    {
        public string Name { get; init; } = "";

        public string Value { get; init; } = "";
    }

    private sealed record DictionaryTemplateDto
    {
        public ConfigurationValueKind KeyKind { get; init; }

        public string? KeyRegexPattern { get; init; }

        public bool? DisallowColonInKey { get; init; }

        public required NodeDto ValueTemplate { get; init; }
    }

    private sealed record ListTemplateDto
    {
        public bool AllowDuplicateItems { get; init; }

        public required NodeDto ItemTemplate { get; init; }

        public string? ItemKeyPropertyName { get; init; }
    }

    private sealed record RuleDto
    {
        public RuleKind? Kind { get; init; }

        public string? ErrorMessage { get; init; }

        public decimal? Min { get; init; }

        public decimal? Max { get; init; }

        public string? Pattern { get; init; }

        public IReadOnlyList<string>? Values { get; init; }

        public int Length { get; init; }
    }

    private enum RuleKind
    {
        Required,
        Range,
        Regex,
        AllowedValues,
        MaxLength,
        MinLength
    }
}
