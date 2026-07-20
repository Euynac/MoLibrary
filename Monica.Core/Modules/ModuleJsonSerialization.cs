using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.Core.JsonSerialization.Annotations;
using Monica.Core.JsonSerialization.Services;
using Monica.Core.JsonSerialization.Services.Support;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleJsonSerializationBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the JsonSerialization module.
        /// </summary>
        public ModuleJsonSerializationGuide AddJsonSerialization(Action<ModuleJsonSerializationOption>? action = null)
        {
            return builder.AddModule<ModuleJsonSerialization, ModuleJsonSerializationOption, ModuleJsonSerializationGuide>(action);
        }
    }
}

[ModuleKey(BuiltInModuleKey.JsonSerialization)]
public class ModuleJsonSerialization(ModuleJsonSerializationOption option)
    : ModuleBase<ModuleJsonSerialization, ModuleJsonSerializationOption, ModuleJsonSerializationGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        var jsonSerializerOptions = new JsonSerializerOptions();
        jsonSerializerOptions.ApplyJsonSerializationDefaults(Option);
        Option.ExtendAction?.Invoke(jsonSerializerOptions);

        if (Application.Modules.TryGetModuleRequestInfo(
                typeof(ModuleResultEnvelope),
                out var resultEnvelopeRegistration))
        {
            ((ModuleResultEnvelopeOption)resultEnvelopeRegistration.ModuleOption)
                .FieldNames
                .ApplyTo(jsonSerializerOptions);
        }

        jsonSerializerOptions.TypeInfoResolver = jsonSerializerOptions.GetConfiguredTypeInfoResolver();
        var provider = new JsonSerializerOptionsProvider(jsonSerializerOptions);

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

public class ModuleJsonSerializationGuide : ModuleGuide<ModuleJsonSerialization, ModuleJsonSerializationOption, ModuleJsonSerializationGuide>
{
}

public class ModuleJsonSerializationOption : ModuleOptions<ModuleJsonSerialization>
{
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
