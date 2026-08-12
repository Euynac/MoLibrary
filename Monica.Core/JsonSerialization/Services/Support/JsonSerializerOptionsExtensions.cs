using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Monica.Core.JsonSerialization.Services.Support;

/// <summary>
/// Provides authoritative copy helpers backed by the runtime's <see cref="JsonSerializerOptions" /> copy constructor.
/// </summary>
public static class JsonSerializerOptionsExtensions
{
    /// <summary>
    /// Creates a mutable copy of the supplied serializer options and applies an additional customization.
    /// </summary>
    /// <param name="source">The serializer options to copy.</param>
    /// <param name="configure">The customization applied to the mutable copy.</param>
    /// <returns>A mutable, independently configurable options instance.</returns>
    public static JsonSerializerOptions Copy(
        this JsonSerializerOptions source,
        Action<JsonSerializerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(configure);
        var copy = new JsonSerializerOptions(source);
        configure(copy);
        return copy;
    }

    /// <summary>
    /// Replaces a mutable target with the complete setting set captured by the runtime copy constructor.
    /// </summary>
    /// <param name="target">The mutable target options owned by an adapter.</param>
    /// <param name="source">The authoritative source options.</param>
    /// <remarks>
    /// ASP.NET Core exposes get-only serializer-option instances, so those objects cannot be replaced. Reflection is
    /// intentionally used over the complete public writable property set to avoid Monica maintaining a drifting list
    /// whenever System.Text.Json adds a setting. Converter and resolver-chain collections are copied explicitly.
    /// </remarks>
    public static void CopyFrom(this JsonSerializerOptions target, JsonSerializerOptions source)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        if (target.IsReadOnly)
        {
            throw new InvalidOperationException("Cannot replace read-only JSON serializer options.");
        }

        var snapshot = new JsonSerializerOptions(source);
        foreach (var property in typeof(JsonSerializerOptions).GetProperties())
        {
            if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            if (property.Name == nameof(JsonSerializerOptions.TypeInfoResolver))
            {
                continue;
            }

            property.SetValue(target, property.GetValue(snapshot));
        }

        target.Converters.Clear();
        foreach (var converter in snapshot.Converters)
        {
            target.Converters.Add(converter);
        }

        target.TypeInfoResolverChain.Clear();
        if (snapshot.TypeInfoResolverChain.Count == 0)
        {
            target.TypeInfoResolver = snapshot.TypeInfoResolver;
        }
        else
        {
            foreach (var resolver in snapshot.TypeInfoResolverChain)
            {
                target.TypeInfoResolverChain.Add(resolver);
            }
        }
    }

    internal static JsonSerializerOptions CopyWithoutConverter(
        this JsonSerializerOptions source,
        Type converterType)
    {
        var copy = new JsonSerializerOptions(source);
        for (var index = copy.Converters.Count - 1; index >= 0; index--)
        {
            if (copy.Converters[index].GetType() == converterType)
            {
                copy.Converters.RemoveAt(index);
            }
        }

        return copy;
    }

    internal static IJsonTypeInfoResolver GetConfiguredTypeInfoResolver(this JsonSerializerOptions options)
    {
        return options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver();
    }
}
