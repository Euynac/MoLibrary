using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.JsonSerialization.Services;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Core.Results;
using Monica.Core.Results.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleResultEnvelopeBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the ResultEnvelope module.
        /// </summary>
        public static ModuleResultEnvelopeGuide AddResultEnvelope(Action<ModuleResultEnvelopeOption>? action = null)
        {
            return new ModuleResultEnvelopeGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.ResultEnvelope)]
public class ModuleResultEnvelope(ModuleResultEnvelopeOption option)
    : MoModule<ModuleResultEnvelope, ModuleResultEnvelopeOption, ModuleResultEnvelopeGuide>(option)
{
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleJsonSerializationGuide>().Register();
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        ResultEnvelopeProvider.Projector = Option.Projector;
        ResultEnvelopeProvider.SerializerOptions = JsonSerializerOptionsProvider.SharedSerializerOptions;
    }
}

public class ModuleResultEnvelopeGuide : MoModuleGuide<ModuleResultEnvelope, ModuleResultEnvelopeOption, ModuleResultEnvelopeGuide>
{
}

public class ModuleResultEnvelopeOption : MoModuleOption<ModuleResultEnvelope>
{
    /// <summary>
    /// Gets or sets the optional custom result projector used for outbound API payloads.
    /// </summary>
    public IResultProjector? Projector { get; set; }
}
