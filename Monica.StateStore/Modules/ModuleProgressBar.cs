using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Module;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.StateStore.ProgressBar;

namespace Monica.StateStore.Modules;


public static class ModuleProgressBarBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 ProgressBar 模块
        /// </summary>
        public static ModuleProgressBarGuide AddProgressBar(Action<ModuleProgressBarOption>? action = null)
        {
            return new ModuleProgressBarGuide().Register(action);
        }
    }
}

public class ModuleProgressBar(ModuleProgressBarOption option)
    : MoModuleWithDependencies<ModuleProgressBar, ModuleProgressBarOption, ModuleProgressBarGuide>(option)
{
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.ProgressBar;
    }

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
    /// 使用分布式存储
    /// </summary>
    public bool UseDistributedStateStore { get; set; }
}