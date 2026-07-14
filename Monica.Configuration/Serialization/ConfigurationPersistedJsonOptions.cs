using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Monica.Configuration.Serialization;

/// <summary>
/// Provides JSON serializer options for persisted Monica.Configuration payloads.
/// </summary>
public static class ConfigurationPersistedJsonOptions
{
    /// <summary>
    /// Gets compact options for schema metadata where null and default values are derivable.
    /// </summary>
    public static JsonSerializerOptions CompactSchema { get; } = CreateReadOnlyOptions(
        new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        });

    /// <summary>
    /// Gets readable options for operator-facing JSON while preserving meaningful null/default values.
    /// </summary>
    public static JsonSerializerOptions ReadableValue { get; } = CreateReadOnlyOptions(
        new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        });

    /// <summary>
    /// Gets compact options for stored values where null, false, zero, and empty strings must remain explicit.
    /// </summary>
    public static JsonSerializerOptions CompactValue { get; } = CreateReadOnlyOptions(
        new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

    private static JsonSerializerOptions CreateReadOnlyOptions(JsonSerializerOptions options)
    {
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
