using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.Core.JsonSerialization.Annotations;
using Monica.Core.JsonSerialization.Models;
using Monica.Core.JsonSerialization.Services;
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
        var plan = Option.WireContract;
        var serializerOptions = plan.GetCanonicalOptions();
        var provider = new JsonSerializerOptionsProvider(serializerOptions, Option.DateTimeFormat);

        services.AddHttpContextAccessor();
        services.AddSingleton(plan);
        services.AddSingleton(serializerOptions);
        services.AddSingleton<IJsonSerializerOptionsProvider>(provider);
        services.Configure<JsonOptions>(options =>
        {
            plan.ApplyTo(options.SerializerOptions);
            options.SerializerOptions.MakeReadOnly();
        });
        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
        {
            plan.ApplyTo(options.JsonSerializerOptions);
            options.JsonSerializerOptions.MakeReadOnly();
        });
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
    /// Adds an ordered host contribution to the canonical JSON contract.
    /// </summary>
    /// <param name="configure">The serializer-options contribution to apply after Monica's defaults.</param>
    /// <remarks>
    /// Contributions are replayed into every supported adapter, including typed state-store operations, then the
    /// canonical snapshot becomes read-only. Changing this contract can make existing persisted state incompatible.
    /// </remarks>
    public void ConfigureSerializer(Action<JsonSerializerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        if (_wireContract is null)
        {
            _serializerContributions.Add(configure);
            return;
        }

        _wireContract.Configure(configure);
    }

    private readonly List<Action<JsonSerializerOptions>> _serializerContributions = [];
    private JsonWireContractPlan? _wireContract;

    internal IReadOnlyList<Action<JsonSerializerOptions>> SerializerContributions => _serializerContributions;

    internal JsonWireContractPlan WireContract => _wireContract ??= new JsonWireContractPlan(this);

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
