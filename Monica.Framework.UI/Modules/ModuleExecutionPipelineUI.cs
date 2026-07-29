using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Framework.UI.Localization;
using Monica.Framework.UI.Pages;
using Monica.Framework.UI.UIExecutionPipeline.State;
using Monica.UI.Shell.Models;
using MudBlazor;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Provides host-bound registration for the execution-pipeline catalog UI.
/// </summary>
public static class ModuleExecutionPipelineUIBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the localized execution-pipeline catalog page for the current Monica host.
        /// </summary>
        /// <param name="configure">Optional page configuration.</param>
        /// <returns>The module guide for further host composition.</returns>
        public ModuleExecutionPipelineUIGuide AddExecutionPipelineUI(
            Action<ModuleExecutionPipelineUIOption>? configure = null)
        {
            return builder.AddModule<
                ModuleExecutionPipelineUI,
                ModuleExecutionPipelineUIOption,
                ModuleExecutionPipelineUIGuide>(configure);
        }
    }
}

/// <summary>
/// Registers the execution-pipeline runtime catalog presentation layer.
/// </summary>
[ModuleKey(BuiltInModuleKey.ExecutionPipelineUI)]
public sealed class ModuleExecutionPipelineUI(ModuleExecutionPipelineUIOption option)
    : ModuleBase<ModuleExecutionPipelineUI, ModuleExecutionPipelineUIOption, ModuleExecutionPipelineUIGuide>(option)
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        if (!Option.DisablePage)
        {
            services.AddScoped<ExecutionPipelinePageState>();
        }
    }

    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        if (Option.DisablePage)
        {
            return;
        }

        DependsOnModule<ModuleExecutionPipelineGuide>().Register();
        DependsOnModule<ModuleLocalizationGuide>().Register()
            .AddResource<ExecutionPipelineResource>();
        DependsOnModule<ModuleShellUIGuide>().Register()
            .RegisterUIComponents(registry => registry.RegisterLocalizedPage<UIExecutionPipelinePage, ExecutionPipelineResource>(
                UIExecutionPipelinePage.PAGE_URL,
                "Pages:ExecutionPipeline:Title",
                Icons.Material.Filled.Schema,
                BuiltInNavigationCategoryIds.Infrastructure,
                addToNav: true,
                navOrder: 20));
    }
}

/// <summary>
/// Fluent guide for the execution-pipeline catalog UI module.
/// </summary>
public sealed class ModuleExecutionPipelineUIGuide
    : ModuleGuide<ModuleExecutionPipelineUI, ModuleExecutionPipelineUIOption, ModuleExecutionPipelineUIGuide>
{
}

/// <summary>
/// Configures the execution-pipeline catalog UI module.
/// </summary>
public sealed class ModuleExecutionPipelineUIOption : ModuleOptions<ModuleExecutionPipelineUI>
{
    /// <summary>
    /// Gets or sets whether the execution-pipeline catalog page, its scoped page state, navigation entry, and UI
    /// dependencies are disabled. This does not disable a Core execution pipeline registered independently.
    /// </summary>
    public bool DisablePage { get; set; }
}
