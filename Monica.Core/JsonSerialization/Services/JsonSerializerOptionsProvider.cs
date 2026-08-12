using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.Core.JsonSerialization.Models;

namespace Monica.Core.JsonSerialization.Services;

/// <summary>
/// Exposes the finalized JSON serializer options owned by one Monica host.
/// </summary>
public sealed class JsonSerializerOptionsProvider(
    JsonSerializerOptions serializerOptions,
    DateTimeWireFormat dateTimeFormat) : IJsonSerializerOptionsProvider
{
    /// <inheritdoc />
    public DateTimeWireFormat DateTimeFormat { get; } = dateTimeFormat
        ?? throw new ArgumentNullException(nameof(dateTimeFormat));

    /// <inheritdoc />
    public JsonSerializerOptions SerializerOptions { get; } = serializerOptions
        ?? throw new ArgumentNullException(nameof(serializerOptions));

    /// <inheritdoc />
    [return: NotNullIfNotNull("str")]
    public string? UsingJsonNamePolicy(string? str)
    {
        if(str == null) return null;
        return SerializerOptions.PropertyNamingPolicy?.ConvertName(str) ?? str;
    }

    /// <inheritdoc />
    [return: NotNullIfNotNull("str")]
    public string? UsingJsonDictionaryKeyPolicy(string? str)
    {
        if (str == null) return null;
        return SerializerOptions.DictionaryKeyPolicy?.ConvertName(str) ?? str;
    }
}
