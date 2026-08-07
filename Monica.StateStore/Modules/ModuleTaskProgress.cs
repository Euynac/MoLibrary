using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.StateStore.Abstractions;
using Monica.StateStore.Cancellation.Abstractions;
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
        public ModuleRegistration<ModuleTaskProgress, ModuleTaskProgressOption> AddTaskProgress(Action<ModuleTaskProgressOption>? action = null)
        {
            return builder.AddModule<ModuleTaskProgress, ModuleTaskProgressOption>(action);
        }
    }
}

public class ModuleTaskProgress : MonicaModule<ModuleTaskProgressOption>
{
    public override void ConfigureServices(ModuleContext<ModuleTaskProgressOption> context)
    {
        var services = context.Services;
        if (!Option.UseDistributedStateStore)
        {
            services.AddKeyedSingleton<ICancellationManager>(
                nameof(ModuleTaskProgress),
                static (provider, _) => provider.GetRequiredService<ICancellationManager>());
            services.AddKeyedSingleton<IStateStore>(
                nameof(ModuleTaskProgress),
                static (provider, _) => provider.GetRequiredService<IStateStore>());
        }

        services.AddSingleton<ITaskProgressService, TaskProgressService>();
    }

    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleCancellationManager, ModuleCancellationManagerOption>();
        module.Require<ModuleStateStore, ModuleStateStoreOption>();
    }
}

/// <summary>
/// Configures task-progress provider features.
/// </summary>
public static class ModuleTaskProgressRegistrationExtensions
{
    /// <summary>
    /// Stores progress and cancellation state in the distributed StateStore provider selected for this host.
    /// </summary>
    /// <param name="module">The TaskProgress registration being configured.</param>
    /// <returns>The same host-bound registration.</returns>
    public static ModuleRegistration<ModuleTaskProgress, ModuleTaskProgressOption> UseDistributedState(
        this ModuleRegistration<ModuleTaskProgress, ModuleTaskProgressOption> module)
    {
        module.Require<ModuleCancellationManager, ModuleCancellationManagerOption>()
            .AddKeyedCancellationManager(nameof(ModuleTaskProgress), useDistributed: true);
        module.Require<ModuleStateStore, ModuleStateStoreOption>()
            .AddKeyedCommonStateStore(nameof(ModuleTaskProgress), useDistributed: true);
        return module.Configure(options => options.UseDistributedStateStore = true);
    }
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
    public bool UseDistributedStateStore { get; internal set; }
}
