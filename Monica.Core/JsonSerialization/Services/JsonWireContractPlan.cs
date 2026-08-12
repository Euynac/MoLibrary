using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Monica.Core.JsonSerialization.Services.Support;
using Monica.Core.Results;
using Monica.Modules;

namespace Monica.Core.JsonSerialization.Services;

/// <summary>
/// Compiles one host's ordered API wire-JSON contributions into immutable serializer snapshots.
/// </summary>
public sealed class JsonWireContractPlan
{
    private readonly ModuleJsonSerializationOption _options;
    private readonly List<Action<JsonSerializerOptions>> _contributions;
    private JsonSerializerOptions? _canonicalOptions;

    internal JsonWireContractPlan(ModuleJsonSerializationOption options)
    {
        _options = options;
        _contributions = [.. options.SerializerContributions];
    }

    /// <summary>
    /// Adds a module-owned API wire-JSON contribution before the contract is compiled.
    /// </summary>
    /// <param name="configure">The ordered serializer-options contribution.</param>
    public void Configure(Action<JsonSerializerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        EnsureOpen();
        _contributions.Add(configure);
    }

    /// <summary>
    /// Adds result-envelope field aliases to this host's API wire contract.
    /// </summary>
    /// <param name="fieldNames">The finalized field-name configuration.</param>
    public void ConfigureResultEnvelope(ResultEnvelopeFieldNames fieldNames)
    {
        ArgumentNullException.ThrowIfNull(fieldNames);
        Configure(fieldNames.ApplyTo);
    }

    /// <summary>
    /// Gets the immutable canonical serializer options shared by non-adapter API consumers.
    /// </summary>
    public JsonSerializerOptions GetCanonicalOptions()
    {
        return _canonicalOptions ??= BuildOptions();
    }

    /// <summary>
    /// Replaces an adapter-owned mutable options object with a complete authoritative snapshot of this wire contract.
    /// </summary>
    /// <param name="target">The adapter-owned serializer-options object.</param>
    public void ApplyTo(JsonSerializerOptions target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.IsReadOnly)
        {
            throw new InvalidOperationException("Cannot apply the Monica JSON wire contract to read-only adapter options.");
        }

        target.CopyFrom(GetCanonicalOptions());
    }

    private JsonSerializerOptions BuildOptions()
    {
        var options = new JsonSerializerOptions();
        options.ApplyJsonSerializationDefaults(_options);
        foreach (var contribution in _contributions)
        {
            contribution(options);
        }

        options.TypeInfoResolver ??= new DefaultJsonTypeInfoResolver();
        options.MakeReadOnly();
        return options;
    }

    private void EnsureOpen()
    {
        if (_canonicalOptions is not null)
        {
            throw new InvalidOperationException(
                "The Monica JSON wire contract has already been compiled and cannot accept late contributions.");
        }
    }
}
