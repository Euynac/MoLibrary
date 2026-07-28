using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.ProjectUnits.CodeAnalysis.Abstractions;
using Monica.ProjectUnits.CodeAnalysis.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>Builder extensions for reusable source-level ProjectUnit analysis.</summary>
public static class ModuleProjectUnitCodeAnalysisBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers semantic MSBuild/Roslyn ProjectUnit analysis. Analysis loads project models on demand and never
        /// starts consumer application hosts.
        /// </summary>
        /// <returns>The ProjectUnit code-analysis module guide.</returns>
        public ModuleProjectUnitCodeAnalysisGuide AddProjectUnitCodeAnalysis()
            => builder.AddModule<ModuleProjectUnitCodeAnalysis, ModuleProjectUnitCodeAnalysisOption,
                ModuleProjectUnitCodeAnalysisGuide>();
    }
}

/// <summary>Provides on-demand semantic source analysis for ProjectUnits.</summary>
[ModuleKey("Monica.ProjectUnits.CodeAnalysis")]
public sealed class ModuleProjectUnitCodeAnalysis(ModuleProjectUnitCodeAnalysisOption option)
    : ModuleBase<ModuleProjectUnitCodeAnalysis, ModuleProjectUnitCodeAnalysisOption,
        ModuleProjectUnitCodeAnalysisGuide>(option)
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddSingleton<IProjectUnitSourceAnalyzer, ProjectUnitSourceAnalyzer>();
    }

    /// <inheritdoc />
    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleProjectUnitsGuide>().Register();
    }
}

/// <summary>Configuration guide for source-level ProjectUnit analysis.</summary>
public sealed class ModuleProjectUnitCodeAnalysisGuide
    : ModuleGuide<ModuleProjectUnitCodeAnalysis, ModuleProjectUnitCodeAnalysisOption,
        ModuleProjectUnitCodeAnalysisGuide>;

/// <summary>Options reserved for source-level ProjectUnit analysis configuration.</summary>
public sealed class ModuleProjectUnitCodeAnalysisOption : ModuleOptions<ModuleProjectUnitCodeAnalysis>;
