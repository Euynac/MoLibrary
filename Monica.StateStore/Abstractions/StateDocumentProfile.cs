using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Monica.StateStore.Abstractions;

/// <summary>
/// Defines one immutable, versioned JSON document contract for durable state.
/// </summary>
/// <remarks>
/// State profiles are independent from API wire JSON and provider encoding. A logical store selects one profile at
/// composition, and its Redis or Dapr implementation captures the same immutable snapshot for every operation.
/// Changing <see cref="ContractVersion" /> or serializer behavior can invalidate existing persisted documents and
/// requires an application-owned migration or explicit data reset.
/// </remarks>
public sealed class StateDocumentProfile
{
    private StateDocumentProfile(
        string name,
        string contractVersion,
        JsonSerializerOptions serializerOptions)
    {
        Name = name;
        ContractVersion = contractVersion;
        SerializerOptions = serializerOptions;
    }

    /// <summary>
    /// Gets the stable logical profile name selected by state-store registrations.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the application-managed version of the persisted JSON contract.
    /// </summary>
    public string ContractVersion { get; }

    /// <summary>
    /// Gets the stable profile identity used in diagnostics and migration records.
    /// </summary>
    public string ContractIdentity => $"{Name}@{ContractVersion}";

    /// <summary>
    /// Gets the media type emitted by this profile.
    /// </summary>
    public string MediaType => "application/json";

    /// <summary>
    /// Gets the read-only serializer options captured by this profile.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; }

    /// <summary>
    /// Creates an immutable JSON document profile.
    /// </summary>
    /// <param name="name">The stable logical profile name.</param>
    /// <param name="contractVersion">The application-managed persisted contract version.</param>
    /// <param name="configure">Optional customization applied to the durable JSON defaults.</param>
    /// <returns>An immutable profile safe for concurrent provider use.</returns>
    public static StateDocumentProfile CreateJson(
        string name,
        string contractVersion,
        Action<JsonSerializerOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractVersion);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        configure?.Invoke(options);
        options.MakeReadOnly();
        return new StateDocumentProfile(name, contractVersion, options);
    }

    /// <summary>
    /// Serializes one value according to this durable document contract.
    /// </summary>
    public byte[] SerializeToUtf8Bytes<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);
    }

    /// <summary>
    /// Serializes one value as a JSON string according to this durable document contract.
    /// </summary>
    public string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, SerializerOptions);
    }

    /// <summary>
    /// Deserializes a UTF-8 state document according to this contract.
    /// </summary>
    public T? Deserialize<T>(ReadOnlySpan<byte> utf8Json)
    {
        return JsonSerializer.Deserialize<T>(utf8Json, SerializerOptions);
    }

    /// <summary>
    /// Deserializes a state document according to this contract.
    /// </summary>
    public T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, SerializerOptions);
    }
}
