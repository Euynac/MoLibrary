using System.Text.Json;
using System.Text.Json.Serialization;
using Monica.Core.JsonSerialization.Converters;
using Monica.Modules;

namespace Monica.Core.JsonSerialization;

public static class MoGlobalJsonExtensions
{
    public static void ApplyJsonSerializationDefaults(this JsonSerializerOptions options, ModuleJsonSerializationOption extraOption)
    {
        options.Converters.Add(new NullableDateTimeJsonConverter());
        options.Converters.Add(new MoDateTimeJsonConverter());
        //options.Converters.Add(new StringJsonConverter { HttpContextAccessor = httpContextAccessor });
        //options.Converters.Add(new JsonConverterFactoryForICollection { HttpContextAccessor = httpContextAccessor });
        options.Converters.Add(new NullableGuidJsonConverter());
        //options.Converters.Add(new JsonConverterFactoryForDtoObjectClass { HttpContextAccessor = httpContextAccessor });
        options.Converters.Add(new NullableLongToStringJsonConverter());

        // Add our EnumFormatValue converter if enabled
        if (extraOption.EnableEnumFormatValue)
        {
            options.Converters.Add(new EnumFormatValueJsonConverterFactory());
        }

        if (extraOption.EnableGlobalEnumToString)
        {
            options.Converters.Add(new ExcludeTypesJsonConverterFactory(new JsonStringEnumConverter(),
                [.. extraOption.EnumTypeToIgnore ?? []])); // Enables global enum string/int conversion while allowing type exclusions.
            //options.Converters.Add(new JsonStringEnumConverter()); // Enables global enum string/int conversion without exclusions.
        }

        options.DefaultIgnoreCondition = extraOption.DefaultIgnoreCondition;

        if (extraOption.ReferenceHandlerPreserve)
        {
            options.ReferenceHandler = ReferenceHandler.Preserve;
        }

        options.PropertyNameCaseInsensitive = true;

        // Allow numeric values to be read from string tokens.
        //options.NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString;
        options.NumberHandling = JsonNumberHandling.AllowReadingFromString;

        //options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        //options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = new JsonCamelCaseNamingPolicy();
        options.PropertyNamingPolicy = new JsonCamelCaseNamingPolicy();

        // Allow JSON comments when needed.
        //options.ReadCommentHandling = JsonCommentHandling.Skip;
    }
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
        target.ReadCommentHandling = cloneFromOptions.ReadCommentHandling;
        target.AllowTrailingCommas = cloneFromOptions.AllowTrailingCommas;
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
}
