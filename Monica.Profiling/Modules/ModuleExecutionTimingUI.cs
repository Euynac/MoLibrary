using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Profiling.Localization;
using Monica.Profiling.Pages;
using Monica.Profiling.UIExecutionTiming.State;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleExecutionTimingUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configures the execution-timing UI module.
        /// </summary>
        public ModuleRegistration<ModuleExecutionTimingUI, ModuleExecutionTimingUIOption> AddExecutionTimingUI(
            Action<ModuleExecutionTimingUIOption>? action = null)
        {
            var registration = builder.AddModule<ModuleExecutionTimingUI, ModuleExecutionTimingUIOption>(action);
            registration.Require<ModuleLocalization, ModuleLocalizationOption>()
                .AddResource<ExecutionTimingResource>();
            registration.Require<ModuleShellUI, ModuleShellUIOption>()
                .RegisterUIComponents(registry => registry.RegisterLocalizedPage<UIExecutionTimingPage, ExecutionTimingResource>(
                    UIExecutionTimingPage.PAGE_URL,
                    "Pages:ExecutionTiming:Title",
                    Icons.Material.Filled.Timer,
                    BuiltInNavigationCategoryIds.Monitor,
                    addToNav: true,
                    navOrder: 30));
            return registration;
        }
    }
}

/// <summary>
/// Execution-timing UI module.
/// </summary>
public class ModuleExecutionTimingUI : MonicaModule<ModuleExecutionTimingUIOption>, IUIModule
{
    /// <inheritdoc />
    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleExecutionTiming, ModuleExecutionTimingOption>();
    }

    public override void ConfigureServices(ModuleContext<ModuleExecutionTimingUIOption> context)
    {
        context.Services.AddScoped<ExecutionTimingPageState>();
    }
}

/// <summary>
/// Configuration options for the execution-timing UI module.
/// </summary>
public class ModuleExecutionTimingUIOption : ModuleOptions<ModuleExecutionTimingUI>
{
    /// <summary>
    /// Controls the automatic refresh interval of the execution-timing page in milliseconds.
    /// Set this to 0 to disable timer-based refresh and require manual refresh only.
    /// </summary>
    public int AutoRefreshIntervalMs { get; set; } = 2000;
}
