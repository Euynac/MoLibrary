using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.Core.JsonSerialization.Annotations;
using Monica.Core.JsonSerialization.Models;
using Monica.Core.JsonSerialization.Services;
using Monica.Core.JsonSerialization.Services.Support;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleJsonSerializationBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the JsonSerialization module.
        /// </summary>
        public ModuleRegistration<ModuleJsonSerialization, ModuleJsonSerializationOption> AddJsonSerialization(
            Action<ModuleJsonSerializationOption>? action = null)
        {
            return builder.AddModule<ModuleJsonSerialization, ModuleJsonSerializationOption>(action);
        }
    }
}

public class ModuleJsonSerialization : MonicaModule<ModuleJsonSerializationOption>
{
    public override void ConfigureServices(ModuleContext<ModuleJsonSerializationOption> context)
    {
        var services = context.Services;
        var jsonSerializerOptions = new JsonSerializerOptions();
        jsonSerializerOptions.ApplyJsonSerializationDefaults(Option);
        Option.ExtendAction?.Invoke(jsonSerializerOptions);

        jsonSerializerOptions.TypeInfoResolver = jsonSerializerOptions.GetConfiguredTypeInfoResolver();
        var provider = new JsonSerializerOptionsProvider(jsonSerializerOptions, Option.DateTimeFormat);

        services.AddHttpContextAccessor();

        services.Configure<JsonOptions>(o =>
        {
            o.SerializerOptions.CloneFrom(jsonSerializerOptions);
        });

        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(o =>
        {
            o.JsonSerializerOptions.CloneFrom(jsonSerializerOptions);
        });

        services.AddSingleton(jsonSerializerOptions);
        services.AddSingleton<IJsonSerializerOptionsProvider>(provider);
    }
}

public class ModuleJsonSerializationOption : ModuleOptions<ModuleJsonSerialization>
{
    /// <summary>
    /// Gets or sets the wire representation used for timezone-free <see cref="DateTime" /> values in JSON bodies,
    /// generated RPC queries, and route parameters.
    /// </summary>
    /// <remarks>
    /// The default is <see cref="DateTimeWireFormat.Iso8601WallClock" />. Configure the same value for every host
    /// participating in one RPC contract. This option does not change <see cref="DateTimeOffset" /> serialization.
    /// </remarks>
    public DateTimeWireFormat DateTimeFormat { get; set; } = DateTimeWireFormat.Iso8601WallClock;

    /// <summary>
    /// Gets or sets an action that applies additional host-specific JSON serializer configuration after Monica's defaults.
    /// </summary>
    public Action<JsonSerializerOptions>? ExtendAction { get; set; }

    /// <summary>
    /// Gets or sets a value that determines when properties with default values are ignored during serialization or deserialization.
    /// The default value is <see cref="JsonIgnoreCondition.Never" />.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The property is set to <see cref="JsonIgnoreCondition.Always" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The property is set after serialization or deserialization has occurred,
    /// or <see cref="JsonSerializerOptions.IgnoreNullValues" /> has already been set to <see langword="true" />.
    /// </exception>
    public JsonIgnoreCondition DefaultIgnoreCondition { get; set; }

    /// <summary>
    /// Gets or sets a value that enables reference-preserving JSON serialization.
    /// </summary>
    public bool ReferenceHandlerPreserve { get; set; }

    /// <summary>
    /// Gets or sets enum types to exclude when global enum-to-string serialization is enabled.
    /// </summary>
    public List<Type>? EnumTypeToIgnore { get; set; }

    /// <summary>
    /// Gets or sets a value that enables global enum-to-string serialization.
    /// </summary>
    public bool EnableGlobalEnumToString { get; set; }

    /// <summary>
    /// Gets or sets a value that enables enum formatted-value serialization via <see cref="EnumFormatValueAttribute" />.
    /// </summary>
    public bool EnableEnumFormatValue { get; set; }
}
