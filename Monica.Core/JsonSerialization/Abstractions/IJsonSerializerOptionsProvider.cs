using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Monica.Core.JsonSerialization.Models;

namespace Monica.Core.JsonSerialization.Abstractions;

public interface IJsonSerializerOptionsProvider
{
    /// <summary>
    /// Gets the host-owned wire representation for timezone-free <see cref="DateTime" /> values.
    /// </summary>
    DateTimeWireFormat DateTimeFormat { get; }

    /// <summary>
    /// Gets the shared JSON serializer options used by the application.
    /// </summary>
    JsonSerializerOptions SerializerOptions { get; }

    /// <summary>
    /// Applies the current global JSON naming policy to the provided string.
    /// </summary>
    /// <param name="str">The source string.</param>
    /// <returns>The converted string, or <see langword="null"/> when <paramref name="str"/> is <see langword="null"/>.</returns>
    [return: NotNullIfNotNull("str")]
    string? UsingJsonNamePolicy(string? str);

    /// <summary>
    /// Applies the current global JSON dictionary-key policy to the provided string.
    /// </summary>
    /// <param name="str">The source string.</param>
    /// <returns>The converted string, or <see langword="null"/> when <paramref name="str"/> is <see langword="null"/>.</returns>
    [return: NotNullIfNotNull("str")]
    string? UsingJsonDictionaryKeyPolicy(string? str);
}
