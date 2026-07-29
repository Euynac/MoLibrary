using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Framework.Seeder.Abstractions;
using Monica.Framework.Seeder.Models;
using Monica.Framework.Seeder.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleSeederBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers startup seeders and their awaited execution runner.
        /// </summary>
        public ModuleSeederGuide AddSeeder(Action<ModuleSeederOption>? action = null)
        {
            return builder.AddModule<ModuleSeeder, ModuleSeederOption, ModuleSeederGuide>(action);
        }
    }
}

/// <summary>
/// Discovers startup seeders and runs each one in an isolated dependency-injection scope.
/// </summary>
[ModuleKey(BuiltInModuleKey.Seeder)]
public sealed class ModuleSeeder(ModuleSeederOption option)
    : ModuleBase<ModuleSeeder, ModuleSeederOption, ModuleSeederGuide>(option), IBusinessTypeIterator
{
    private readonly List<Type> _seedTypes = [];

    public override void ClaimDependencies()
    {
        DependsOnModule<ModuleExecutionPipelineGuide>().Register();
    }

    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (type is { IsClass: true, IsAbstract: false }
                && typeof(ISeeder).IsAssignableFrom(type))
            {
                _seedTypes.Add(type);
            }

            yield return type;
        }
    }

    public override void PostConfigureServices(IServiceCollection services)
    {
        var seedTypes = _seedTypes
            .Distinct()
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        foreach (var seedType in seedTypes)
        {
            services.AddTransient(seedType);
        }

        services.AddHostedService(serviceProvider =>
            ActivatorUtilities.CreateInstance<SeederStartupHostedService>(
                serviceProvider,
                seedTypes,
                option.FailureBehavior));
    }
}

/// <summary>
/// Provides fluent configuration for startup seeding.
/// </summary>
public sealed class ModuleSeederGuide
    : ModuleGuide<ModuleSeeder, ModuleSeederOption, ModuleSeederGuide>;

/// <summary>
/// Configures startup seeder execution for one Monica host.
/// </summary>
public sealed class ModuleSeederOption : ModuleOptions<ModuleSeeder>
{
    /// <summary>
    /// Gets or sets how host startup responds when a seeder fails. The default records the failure, continues with
    /// remaining seeders, and allows startup to finish.
    /// </summary>
    public SeederFailureBehavior FailureBehavior { get; set; } = SeederFailureBehavior.ContinueStartup;
}
