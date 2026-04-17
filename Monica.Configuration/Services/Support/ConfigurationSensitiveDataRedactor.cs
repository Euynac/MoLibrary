using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Monica.Configuration.Models;
using Monica.Configuration.Models.Internal;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Centralizes masking and redaction rules for sensitive configuration options.
/// </summary>
public static class ConfigurationSensitiveDataRedactor
{
    /// <summary>
    /// Stable masked placeholder used for sensitive configuration values.
    /// </summary>
    public const string MaskedValue = "******";

    /// <summary>
    /// Determines whether the specified runtime value should be treated as configured.
    /// </summary>
    /// <param name="value">Runtime value to inspect.</param>
    /// <returns><see langword="true"/> when a meaningful value is present; otherwise <see langword="false"/>.</returns>
    public static bool HasStoredValue(object? value)
    {
        return value switch
        {
            null => false,
            string stringValue => HasStoredValue(stringValue),
            JsonNode jsonNode => HasStoredValue(jsonNode),
            JsonElement jsonElement => HasStoredValue(jsonElement),
            _ => true
        };
    }

    /// <summary>
    /// Determines whether the specified JSON value should be treated as configured.
    /// </summary>
    /// <param name="value">JSON value to inspect.</param>
    /// <returns><see langword="true"/> when a meaningful value is present; otherwise <see langword="false"/>.</returns>
    public static bool HasStoredValue(JsonNode? value)
    {
        if (value == null)
        {
            return false;
        }

        return value.GetValueKind() switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            JsonValueKind.String => HasStoredValue(value.GetValue<string>()),
            _ => true
        };
    }

    /// <summary>
    /// Determines whether the specified JSON element should be treated as configured.
    /// </summary>
    /// <param name="value">JSON element to inspect.</param>
    /// <returns><see langword="true"/> when a meaningful value is present; otherwise <see langword="false"/>.</returns>
    public static bool HasStoredValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => false,
            JsonValueKind.String => HasStoredValue(value.GetString()),
            _ => true
        };
    }

    /// <summary>
    /// Redacts sensitive properties inside a configuration JSON payload identified by configuration key.
    /// </summary>
    /// <param name="key">Configuration key.</param>
    /// <param name="value">Configuration JSON payload.</param>
    /// <returns>A cloned JSON payload with sensitive fields replaced by masked placeholders.</returns>
    public static JsonNode? RedactConfigurationNode(string key, JsonNode? value)
    {
        if (value == null)
        {
            return null;
        }

        var configKey = key.Split(':').FirstOrDefault() ?? key;
        if (!ConfigurationRegistration.TryGetConfig(configKey, out var config))
        {
            return CloneJsonNode(value);
        }

        if (CloneJsonNode(value) is not JsonObject clone)
        {
            return CloneJsonNode(value);
        }

        foreach (var option in config.OptionItems.Where(option => option.IsSensitive))
        {
            if (!clone.TryGetPropertyValue(option.Name, out var optionValue))
            {
                continue;
            }

            if (!HasStoredValue(optionValue))
            {
                continue;
            }

            clone[option.Name] = JsonValue.Create(MaskedValue);
        }

        return clone;
    }

    /// <summary>
    /// Redacts sensitive fields in configuration history records.
    /// </summary>
    /// <param name="entries">Raw history entries.</param>
    /// <returns>Redacted history entries safe for management APIs.</returns>
    public static List<ConfigurationHistoryEntry> RedactHistoryEntries(IEnumerable<ConfigurationHistoryEntry> entries)
    {
        return entries.Select(RedactHistoryEntry).ToList();
    }

    /// <summary>
    /// Redacts sensitive fields in a single configuration history entry.
    /// </summary>
    /// <param name="entry">Raw history entry.</param>
    /// <returns>Redacted history entry safe for management APIs.</returns>
    public static ConfigurationHistoryEntry RedactHistoryEntry(ConfigurationHistoryEntry entry)
    {
        return new ConfigurationHistoryEntry
        {
            Title = entry.Title,
            AppId = entry.AppId,
            Key = entry.Key,
            OldValue = RedactConfigurationNode(entry.Key, entry.OldValue),
            NewValue = RedactConfigurationNode(entry.Key, entry.NewValue),
            ModificationTime = entry.ModificationTime,
            ModifierId = entry.ModifierId,
            Username = entry.Username,
            Version = entry.Version
        };
    }

    /// <summary>
    /// Redacts sensitive values in configuration provider diagnostics.
    /// </summary>
    /// <param name="groups">Raw provider groups.</param>
    /// <returns>Redacted provider groups safe for management APIs.</returns>
    public static List<ConfigurationProviderGroup> RedactProviderGroups(IEnumerable<ConfigurationProviderGroup> groups)
    {
        return groups.Select(group => new ConfigurationProviderGroup
        {
            GroupName = group.GroupName,
            Providers = group.Providers.Select(provider => new ConfigurationProviderSnapshot
            {
                Name = provider.Name,
                Type = provider.Type,
                ConfigurationData = provider.ConfigurationData.ToDictionary(
                    pair => pair.Key,
                    pair => ShouldRedactKey(pair.Key) && HasStoredValue(pair.Value) ? MaskedValue : pair.Value)
            }).ToList()
        }).ToList();
    }

    /// <summary>
    /// Redacts sensitive values in configuration debug view output.
    /// </summary>
    /// <param name="debugView">Raw debug view string.</param>
    /// <returns>Redacted debug view string safe for management APIs.</returns>
    public static string RedactDebugView(string debugView)
    {
        if (string.IsNullOrWhiteSpace(debugView))
        {
            return debugView;
        }

        var sensitiveKeys = GetSensitiveKeys();
        if (sensitiveKeys.Count == 0)
        {
            return debugView;
        }

        var lines = debugView.Split(Environment.NewLine);
        for (var index = 0; index < lines.Length; index++)
        {
            foreach (var key in sensitiveKeys)
            {
                if (!TryRedactDebugLine(lines[index], key, out var redactedLine))
                {
                    continue;
                }

                lines[index] = redactedLine;
                break;
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Creates a deep clone of a JSON node.
    /// </summary>
    /// <param name="value">Source node.</param>
    /// <returns>Cloned node, or <see langword="null"/> when the source is null.</returns>
    public static JsonNode? CloneJsonNode(JsonNode? value)
    {
        if (value == null)
        {
            return null;
        }

        return JsonNode.Parse(value.ToJsonString());
    }

    private static bool HasStoredValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool ShouldRedactKey(string key)
    {
        return ConfigurationRegistration.TryGetOptionItem(key, out var option) && option.IsSensitive;
    }

    private static List<string> GetSensitiveKeys()
    {
        return ConfigurationRegistration.Cards.Values
            .SelectMany(card => card.Configuration.OptionItems)
            .Where(option => option.IsSensitive)
            .Select(option => option.Key)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(key => key.Length)
            .ToList();
    }

    private static bool TryRedactDebugLine(string line, string key, out string redactedLine)
    {
        var pattern = $@"^(?<prefix>\s*{Regex.Escape(key)}\s*=)(?<value>.*?)(?<suffix>\s*(?:\(.+\))?)$";
        var match = Regex.Match(line, pattern, RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            redactedLine = line;
            return false;
        }

        if (!HasStoredValue(match.Groups["value"].Value))
        {
            redactedLine = line;
            return true;
        }

        redactedLine = $"{match.Groups["prefix"].Value}{MaskedValue}{match.Groups["suffix"].Value}";
        return true;
    }
}
