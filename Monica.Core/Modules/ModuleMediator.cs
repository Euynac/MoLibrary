using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Tool.Results;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleMediatorBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configures the Mediator module.
        /// </summary>
        public static ModuleMediatorGuide AddMediator(Action<ModuleMediatorOption>? action = null)
        {
            return new ModuleMediatorGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.Mediator)]
public class ModuleMediator(ModuleMediatorOption option) : MoModule<ModuleMediator, ModuleMediatorOption, ModuleMediatorGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        // TODO: move this into the shared business-type iteration pipeline.
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<IResultEnvelope>();
            cfg.RegisterServicesFromAssembly(Assembly.GetEntryAssembly()!);
        });
    }
}

public class ModuleMediatorGuide : MoModuleGuide<ModuleMediator, ModuleMediatorOption, ModuleMediatorGuide>
{

}

public class ModuleMediatorOption : MoModuleOption<ModuleMediator>
{
}
