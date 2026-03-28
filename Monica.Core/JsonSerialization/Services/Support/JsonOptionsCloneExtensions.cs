using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Monica.Core.JsonSerialization.Services.Support;

public static class JsonOptionsCloneExtensions
{
    public static JsonSerializerOptions Clone(this JsonSerializerOptions target, Action<JsonSerializerOptions> optionAction)
    {
        var cloned = new JsonSerializerOptions();
        cloned.CloneFrom(target);
        optionAction(cloned);
        return cloned;
    }

    public static JsonSerializerOptions Clone(this JsonSerializerOptions target)
    {
        var cloned = new JsonSerializerOptions();
        cloned.CloneFrom(target);
        return cloned;
    }

    public static void CloneFrom(this JsonSerializerOptions target, JsonSerializerOptions cloneFromOptions)
    {
        target.Converters.Clear();
        foreach (var converter in cloneFromOptions.Converters)
        {
            target.Converters.Add(converter);
        }

        target.PropertyNamingPolicy = cloneFromOptions.PropertyNamingPolicy;
        target.PropertyNameCaseInsensitive = cloneFromOptions.PropertyNameCaseInsensitive;
        target.DefaultIgnoreCondition = cloneFromOptions.DefaultIgnoreCondition;
        target.WriteIndented = cloneFromOptions.WriteIndented;
        target.Encoder = cloneFromOptions.Encoder;
        target.DefaultBufferSize = cloneFromOptions.DefaultBufferSize;
        target.DictionaryKeyPolicy = cloneFromOptions.DictionaryKeyPolicy;
        target.IgnoreReadOnlyProperties = cloneFromOptions.IgnoreReadOnlyProperties;
        target.IncludeFields = cloneFromOptions.IncludeFields;
        target.MaxDepth = cloneFromOptions.MaxDepth;
        target.NumberHandling = cloneFromOptions.NumberHandling;
        target.ReferenceHandler = cloneFromOptions.ReferenceHandler;
        target.ReadCommentHandling = cloneFromOptions.ReadCommentHandling;
        target.AllowTrailingCommas = cloneFromOptions.AllowTrailingCommas;
        target.TypeInfoResolver = cloneFromOptions.GetConfiguredTypeInfoResolver();
    }

    internal static JsonSerializerOptions CloneButFilterConverter(this JsonSerializerOptions cloneFromOptions, Type filteredConverter)
    {
        var clonedOptions = new JsonSerializerOptions(cloneFromOptions);
        for (var index = 0; index < clonedOptions.Converters.Count; index++)
        {
            var converter = clonedOptions.Converters[index];
            if (converter.GetType() == filteredConverter)
            {
                clonedOptions.Converters.Remove(converter);
            }
        }

        return clonedOptions;
    }

    internal static IJsonTypeInfoResolver GetConfiguredTypeInfoResolver(this JsonSerializerOptions options)
    {
        return options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver();
    }
}
