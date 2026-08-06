using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models;
using Monica.Core.TypeDiscovery.Models;
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
        public ModuleRegistration<ModuleSeeder, ModuleSeederOption> AddSeeder(Action<ModuleSeederOption>? action = null)
        {
            return builder.AddModule<ModuleSeeder, ModuleSeederOption>(action);
        }
    }
}

/// <summary>
/// Discovers startup seeders and runs each one in an isolated dependency-injection scope.
/// </summary>
public sealed class ModuleSeeder : MonicaModule<ModuleSeederOption>
{
    private IReadOnlyList<Type> _seedTypes = [];

    public override void Describe(ModuleDescriptor module)
    {
        module.Require<ModuleExecutionPipeline, ModuleExecutionPipelineOption>();
    }

    /// <inheritdoc />
    public override void DiscoverTypes(TypeDiscoveryPlan<ModuleSeederOption> discovery)
    {
        discovery.Match(
            TypeQuery.ConcreteClass.AssignableTo<ISeeder>(),
            (context, matches) =>
            {
                _seedTypes = matches
                    .Select(static match => match.Type)
                    .OrderBy(static type => type.FullName, StringComparer.Ordinal)
                    .ToArray();

                foreach (var seedType in _seedTypes)
                {
                    context.Registrations.Add(ServiceDescriptor.Transient(seedType, seedType));
                }
            });
    }

    public override void PostConfigureServices(ModuleContext<ModuleSeederOption> context)
    {
        context.Services.AddHostedService(serviceProvider =>
            ActivatorUtilities.CreateInstance<SeederStartupHostedService>(
                serviceProvider,
                _seedTypes,
                Option.FailureBehavior));
    }
}

/// <summary>
/// Provides fluent configuration for startup seeding.
/// </summary>


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
