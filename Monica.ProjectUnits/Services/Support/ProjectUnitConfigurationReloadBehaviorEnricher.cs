using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.ProjectUnits.Abstractions;
using Monica.ProjectUnits.Models;

namespace Monica.ProjectUnits.Services.Support;

/// <summary>
/// Applies ProjectUnits options-usage analysis to Monica.Configuration definitions when both modules are present.
/// </summary>
internal static class ProjectUnitConfigurationReloadBehaviorEnricher
{
    /// <summary>
    /// Enriches unknown configuration reload behavior from discovered dependency-injection options usage.
    /// </summary>
    /// <param name="services">The service collection populated by registered Monica modules.</param>
    /// <param name="catalog">The host-owned project-unit catalog.</param>
    /// <param name="logger">The project-units module logger.</param>
    public static void Enrich(IServiceCollection services, IProjectUnitCatalog catalog, ILogger logger)
    {
        var definitionRegistry = ResolveDefinitionRegistry(services);
        if (definitionRegistry is null)
        {
            return;
        }

        foreach (var configurationUnit in catalog.GetUnits<UnitConfiguration>())
        {
            EnrichDefinition(definitionRegistry, configurationUnit, logger);
        }
    }

    private static void EnrichDefinition(
        IConfigurationDefinitionRegistry definitionRegistry,
        UnitConfiguration configurationUnit,
        ILogger logger)
    {
        if (!definitionRegistry.TryGet(configurationUnit.DefinitionKey, out var definition)
            || definition is null)
        {
            return;
        }

        if (configurationUnit.InferredReloadBehavior is not { } inferredBehavior)
        {
            if (configurationUnit.ConfigurationDependencies.Count == 0
                && definition.ReloadBehaviorObservationKind != ConfigurationReloadBehaviorObservationKind.Declared)
            {
                definitionRegistry.Register(definition with
                {
                    ReloadBehavior = ConfigurationReloadBehavior.Unknown,
                    ReloadBehaviorObservationKind = ConfigurationReloadBehaviorObservationKind.NotConsumed
                });
            }

            return;
        }

        if (definition.ReloadBehaviorObservationKind != ConfigurationReloadBehaviorObservationKind.Declared)
        {
            definitionRegistry.Register(definition with
            {
                ReloadBehavior = inferredBehavior,
                ReloadBehaviorObservationKind = ConfigurationReloadBehaviorObservationKind.Inferred
            });
            return;
        }

        if (definition.ReloadBehavior == inferredBehavior)
        {
            return;
        }

        var message =
            $"Configuration '{configurationUnit.DefinitionKey}' explicitly declares reload behavior '{definition.ReloadBehavior}', " +
            $"but ProjectUnits inferred '{inferredBehavior}' from options usage.";
        configurationUnit.Alerts.Add(new ProjectUnitAlert
        {
            Level = EAlertLevel.Warning,
            Message = message,
            Source = "ConfigurationReloadBehaviorInference"
        });
        logger.LogWarning(
            "Configuration '{DefinitionKey}' explicitly declares reload behavior '{ExplicitBehavior}', but ProjectUnits inferred '{InferredBehavior}' from options usage.",
            configurationUnit.DefinitionKey,
            definition.ReloadBehavior,
            inferredBehavior);
    }

    private static IConfigurationDefinitionRegistry? ResolveDefinitionRegistry(IServiceCollection services)
    {
        return services
            .LastOrDefault(descriptor =>
                !descriptor.IsKeyedService
                && descriptor.ServiceType == typeof(IConfigurationDefinitionRegistry))
            ?.ImplementationInstance as IConfigurationDefinitionRegistry;
    }
}
