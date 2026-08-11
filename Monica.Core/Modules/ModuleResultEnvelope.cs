using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.JsonSerialization.Extensions;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
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
        public ModuleRegistration<ModuleResultEnvelope, ModuleResultEnvelopeOption> AddResultEnvelope(
            Action<ModuleResultEnvelopeOption>? action = null)
        {
            return builder.AddModule<ModuleResultEnvelope, ModuleResultEnvelopeOption>(action);
        }
    }

    extension(ModuleRegistration<ModuleResultEnvelope, ModuleResultEnvelopeOption> registration)
    {
        /// <summary>
        /// Configures top-level JSON field names for Monica result envelopes.
        /// </summary>
        public ModuleRegistration<ModuleResultEnvelope, ModuleResultEnvelopeOption> UseResultFieldNames(
            Action<ResultEnvelopeFieldNames> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            return registration.Configure(option => configure(option.FieldNames));
        }

        /// <summary>
        /// Limits how much remote request or response content is kept for diagnostics.
        /// </summary>
        public ModuleRegistration<ModuleResultEnvelope, ModuleResultEnvelopeOption> SetMaxRemoteDiagnosticBodyBytes(
            int maxBodyBytes)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maxBodyBytes);
            return registration.Configure(option => option.MaxRemoteDiagnosticBodyBytes = maxBodyBytes);
        }
    }
}

public class ModuleResultEnvelope : MonicaModule<ModuleResultEnvelopeOption>
{
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleJsonSerialization, ModuleJsonSerializationOption>();
    }

    public override void ConfigureServices(ModuleContext<ModuleResultEnvelopeOption> context)
    {
        var services = context.Services;
        services.AddSingleton<IResultEnvelopeReader, ResultEnvelopeProvider>();

        // ResultEnvelope composes after JsonSerialization. Apply its wire names to the single
        // host-owned options instance so HTTP endpoints, Dapr, and generated RPC clients all
        // serialize and deserialize the same result-envelope contract.
        Option.FieldNames.ApplyTo(services.GetMonicaJsonSerializerOptions());
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
