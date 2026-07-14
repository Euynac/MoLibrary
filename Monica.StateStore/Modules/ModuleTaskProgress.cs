using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.StateStore.TaskProgress.Abstractions;
using Monica.StateStore.TaskProgress.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleTaskProgressBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the task progress module.
        /// </summary>
        public ModuleTaskProgressGuide AddTaskProgress(Action<ModuleTaskProgressOption>? action = null)
        {
            return builder.AddModule<ModuleTaskProgress, ModuleTaskProgressOption, ModuleTaskProgressGuide>(action);
        }
    }
}

[ModuleKey(BuiltInModuleKey.TaskProgress)]
public class ModuleTaskProgress(ModuleTaskProgressOption option)
    : ModuleBase<ModuleTaskProgress, ModuleTaskProgressOption, ModuleTaskProgressGuide>(option)
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ITaskProgressService, TaskProgressService>();
    }

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleCancellationManagerGuide>()
            .Register()
            .AddKeyedCancellationManager(nameof(ModuleTaskProgress), Option.UseDistributedStateStore);
        DependsOnModule<ModuleStateStoreGuide>()
            .Register()
            .AddKeyedCommonStateStore(nameof(ModuleTaskProgress), Option.UseDistributedStateStore);
    }
}

/// <summary>
/// Guides configuration for the task progress module.
/// </summary>
public class ModuleTaskProgressGuide : ModuleGuide<ModuleTaskProgress, ModuleTaskProgressOption, ModuleTaskProgressGuide>
{
}

/// <summary>
/// Configures how the task progress module stores distributed progress state.
/// </summary>
public class ModuleTaskProgressOption : ModuleOptions<ModuleTaskProgress>
{
    /// <summary>
    /// Enables distributed state storage for task progress snapshots instead of process-local storage.
    /// Configure this when progress must be shared across instances or recovered by another process.
    /// </summary>
    public bool UseDistributedStateStore { get; set; }
}
