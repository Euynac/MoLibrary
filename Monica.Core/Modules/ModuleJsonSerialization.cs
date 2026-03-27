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
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleJsonSerializationBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the JsonSerialization module.
        /// </summary>
        public static ModuleJsonSerializationGuide AddJsonSerialization(Action<ModuleJsonSerializationOption>? action = null)
        {
            return new ModuleJsonSerializationGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.JsonSerialization)]
public class ModuleJsonSerialization(ModuleJsonSerializationOption option)
    : MoModule<ModuleJsonSerialization, ModuleJsonSerializationOption, ModuleJsonSerializationGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        var jsonSerializerOptions = new JsonSerializerOptions();
        jsonSerializerOptions.ApplyJsonSerializationDefaults(Option);
        Option.ExtendAction?.Invoke(jsonSerializerOptions);
        JsonSerializerOptionsProvider.SharedSerializerOptions = jsonSerializerOptions;

        services.AddHttpContextAccessor();

        services.Configure<JsonOptions>(o =>
        {
            o.SerializerOptions.CloneFrom(JsonSerializerOptionsProvider.SharedSerializerOptions);
        });

        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(o =>
        {
            o.JsonSerializerOptions.CloneFrom(JsonSerializerOptionsProvider.SharedSerializerOptions);
        });

        services.AddSingleton<IJsonSerializerOptionsProvider, JsonSerializerOptionsProvider>();
    }
}

public class ModuleJsonSerializationGuide : MoModuleGuide<ModuleJsonSerialization, ModuleJsonSerializationOption, ModuleJsonSerializationGuide>
{
}

public class ModuleJsonSerializationOption : MoModuleOption<ModuleJsonSerialization>
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
