using System.Text.Json;
using Monica.Configuration.Serialization;

namespace Monica.Configuration.EfCore.Stores.Support;

internal static class ConfigurationPersistenceValueConverter
{
    internal static DateTimeOffset ToUtcOffset(DateTime value)
    {
        var utcValue = value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return new DateTimeOffset(utcValue);
    }

    internal static string NormalizeJson(string json)
    {
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        return JsonSerializer.Serialize(document.RootElement, ConfigurationPersistedJsonOptions.CompactValue);
    }
}
