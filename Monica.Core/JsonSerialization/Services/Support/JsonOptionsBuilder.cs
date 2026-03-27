using System.Text.Json;
using System.Text.Json.Serialization;
using Monica.Core.JsonSerialization.Converters;
using Monica.Modules;

namespace Monica.Core.JsonSerialization.Services.Support;

internal static class JsonOptionsBuilder
{
    public static void ApplyJsonSerializationDefaults(this JsonSerializerOptions options, ModuleJsonSerializationOption extraOption)
    {
        options.Converters.Add(new NullableDateTimeJsonConverter());
        options.Converters.Add(new DateTimeJsonConverter());
        options.Converters.Add(new NullableGuidJsonConverter());
        options.Converters.Add(new LongToStringJsonConverter());

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
}
