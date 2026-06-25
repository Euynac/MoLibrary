using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Framework.ProjectUnits.Models;

namespace Monica.Framework.ProjectUnits.Services.Support;

/// <summary>
/// Applies ProjectUnits options-usage analysis to Monica.Configuration definitions when both modules are present.
/// </summary>
internal static class ProjectUnitConfigurationReloadBehaviorEnricher
{
    /// <summary>
    /// Enriches unknown configuration reload behavior from discovered dependency-injection options usage.
    /// </summary>
    /// <param name="services">The service collection populated by registered Monica modules.</param>
    /// <param name="logger">The project-units module logger.</param>
    public static void Enrich(IServiceCollection services, ILogger logger)
    {
        var definitionRegistry = ResolveDefinitionRegistry(services);
        if (definitionRegistry is null)
        {
            return;
        }

        foreach (var configurationUnit in ProjectUnitRegistry.GetUnits<UnitConfiguration>())
        {
            EnrichDefinition(definitionRegistry, configurationUnit, logger);
        }
    }

    private static void EnrichDefinition(
        IConfigurationDefinitionRegistry definitionRegistry,
        UnitConfiguration configurationUnit,
        ILogger logger)
    {
        if (configurationUnit.InferredReloadBehavior is not { } inferredBehavior
            || !definitionRegistry.TryGet(configurationUnit.DefinitionKey, out var definition)
            || definition is null)
        {
            return;
        }

        if (definition.ReloadBehavior == ConfigurationReloadBehavior.Unknown)
        {
            definitionRegistry.Register(definition with { ReloadBehavior = inferredBehavior });
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
