using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.StateStore.ProgressBar;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleProgressBarBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the ProgressBar module
        /// </summary>
        public static ModuleProgressBarGuide AddProgressBar(Action<ModuleProgressBarOption>? action = null)
        {
            return new ModuleProgressBarGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.ProgressBar)]
public class ModuleProgressBar(ModuleProgressBarOption option)
    : MoModule<ModuleProgressBar, ModuleProgressBarOption, ModuleProgressBarGuide>(option)
{

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IMoProgressBarService, MoProgressBarService>();
        services.AddHostedService<MoProgressBarService>();
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleCancellationManagerGuide>().Register().AddKeyedCancellationManager(nameof(ModuleProgressBar), Option.UseDistributedStateStore);
        DependsOnModule<ModuleStateStoreGuide>().Register().AddKeyedCommonStateStore(nameof(ModuleProgressBar), Option.UseDistributedStateStore);
    }
}

public class ModuleProgressBarGuide : MoModuleGuide<ModuleProgressBar, ModuleProgressBarOption, ModuleProgressBarGuide>
{

   
}

public class ModuleProgressBarOption : MoModuleOption<ModuleProgressBar>
{
    /// <summary>
    /// Use distributed storage
    /// </summary>
    public bool UseDistributedStateStore { get; set; }
}