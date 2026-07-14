using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.Core.Results.Abstractions;
using Monica.Core.Results.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleResultEnvelopeBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the ResultEnvelope module.
        /// </summary>
        public ModuleResultEnvelopeGuide AddResultEnvelope(Action<ModuleResultEnvelopeOption>? action = null)
        {
            return builder.AddModule<ModuleResultEnvelope, ModuleResultEnvelopeOption, ModuleResultEnvelopeGuide>(action);
        }
    }
}

[ModuleKey(BuiltInModuleKey.ResultEnvelope)]
public class ModuleResultEnvelope(ModuleResultEnvelopeOption option)
    : ModuleBase<ModuleResultEnvelope, ModuleResultEnvelopeOption, ModuleResultEnvelopeGuide>(option)
{
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleJsonSerializationGuide>().Register();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IResultEnvelopeReader, ResultEnvelopeProvider>();
    }
}

public class ModuleResultEnvelopeGuide : ModuleGuide<ModuleResultEnvelope, ModuleResultEnvelopeOption, ModuleResultEnvelopeGuide>
{
    /// <summary>
    /// Configures top-level JSON field names for Monica result envelopes.
    /// Configured names are treated like property identifiers and are normalized by the current JSON <see cref="System.Text.Json.JsonSerializerOptions.PropertyNamingPolicy" />.
    /// For example, under camel-case naming, configuring <c>StatusCode</c> produces <c>statusCode</c>.
    /// </summary>
    /// <param name="configure">The field-name configuration action.</param>
    /// <returns>The current guide instance.</returns>
    public ModuleResultEnvelopeGuide UseResultFieldNames(Action<ResultEnvelopeFieldNames> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        ConfigureModuleOption(option => configure(option.FieldNames));
        return this;
    }

    /// <summary>
    /// Limits how much remote request or response content is kept for diagnostics.
    /// </summary>
    /// <param name="maxBodyBytes">The maximum number of bytes to capture.</param>
    /// <returns>The current guide instance.</returns>
    public ModuleResultEnvelopeGuide SetMaxRemoteDiagnosticBodyBytes(int maxBodyBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxBodyBytes);

        ConfigureModuleOption(option => option.MaxRemoteDiagnosticBodyBytes = maxBodyBytes);
        return this;
    }
}

public class ModuleResultEnvelopeOption : ModuleOptions<ModuleResultEnvelope>
{
    /// <summary>
    /// Gets the top-level JSON field names used for Monica result envelopes.
    /// </summary>
    public ResultEnvelopeFieldNames FieldNames { get; set; } = new();

    /// <summary>
    /// Gets or sets the maximum number of request or response bytes captured for remote diagnostics.
    /// </summary>
    public int MaxRemoteDiagnosticBodyBytes { get; set; } = 32 * 1024;
}
