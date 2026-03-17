using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Monica.Core.JsonSerialization.Interfaces;

public interface IJsonSerializerOptionsProvider
{
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
}
