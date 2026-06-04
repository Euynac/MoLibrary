using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Monica.Configuration.Annotations;

namespace Monica.Configuration.Bootstrap;

/// <summary>
/// Describes a CLR options type that can be read during host bootstrap because it is marked with
/// <see cref="ConfigurationAttribute"/>.
/// </summary>
public sealed record MonicaBootstrapConfigurationType
{
    /// <summary>
    /// Gets the CLR type marked with <see cref="ConfigurationAttribute"/>.
    /// </summary>
    public required Type ClrType { get; init; }

    /// <summary>
    /// Gets the stable Monica configuration definition key.
    /// </summary>
    public required string DefinitionKey { get; init; }

    /// <summary>
    /// Gets the Microsoft configuration section path used for bootstrap binding.
    /// </summary>
    public required string SectionPath { get; init; }

    /// <summary>
    /// Gets the display name configured for management UI and diagnostics.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the owning module or application component when one is declared.
    /// </summary>
    public string? OwnerModule { get; init; }

    /// <summary>
    /// Gets the developer-defined category when one is declared.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets the developer-facing description when one is declared.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Represents a bootstrap binding result for a Monica configuration type.
/// </summary>
public sealed record MonicaBootstrapConfigurationBinding
{
    /// <summary>
    /// Gets the discovered Monica configuration type metadata.
    /// </summary>
    public required MonicaBootstrapConfigurationType ConfigurationType { get; init; }

    /// <summary>
    /// Gets the bound options object.
    /// </summary>
    public required object Value { get; init; }

    /// <summary>
    /// Gets whether the helper had to create CLR defaults because the configuration section was missing or failed to bind.
    /// </summary>
    public bool UsedClrDefaults { get; init; }
}

/// <summary>
/// Provides bootstrap-time helpers for reading Monica <see cref="ConfigurationAttribute"/> options before
/// the Monica effective-value store has been activated.
/// </summary>
public static class MonicaConfigurationBootstrapExtensions
{
    /// <summary>
    /// Binds a Monica configuration options type from the host bootstrap <see cref="IConfiguration"/>.
    /// </summary>
    /// <typeparam name="TOptions">The options type marked with <see cref="ConfigurationAttribute"/>.</typeparam>
    /// <param name="configuration">The host configuration available during application builder setup.</param>
    /// <param name="logger">Optional logger used to emit warnings for missing attributes, missing sections, and binding failures.</param>
    /// <returns>
    /// The bound options object, or a CLR default instance when the section is unavailable or cannot be bound.
    /// </returns>
    /// <remarks>
    /// This helper is intended for bootstrap settings such as database connectivity that must be available before
    /// Monica-managed configuration is loaded. Runtime-managed settings should still flow through Monica.Configuration.
    /// </remarks>
    public static TOptions GetMonicaBootstrapConfiguration<TOptions>(
        this IConfiguration configuration,
        ILogger? logger = null)
        where TOptions : class, new()
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configurationType = CreateConfigurationType(typeof(TOptions), logger);
        if (configurationType is null)
        {
            return new TOptions();
        }

        var binding = BindConfiguration(configuration, configurationType, () => new TOptions(), logger);
        return (TOptions)binding.Value;
    }

    /// <summary>
    /// Discovers all non-abstract classes marked with <see cref="ConfigurationAttribute"/> from the supplied assemblies.
    /// </summary>
    /// <param name="assemblies">Assemblies that should be scanned for Monica configuration types.</param>
    /// <param name="logger">Optional logger used to emit warnings for assemblies that cannot be fully scanned.</param>
    /// <returns>The discovered Monica bootstrap configuration type metadata.</returns>
    public static IReadOnlyList<MonicaBootstrapConfigurationType> DiscoverMonicaBootstrapConfigurationTypes(
        this IEnumerable<Assembly> assemblies,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var result = new List<MonicaBootstrapConfigurationType>();
        var seenTypes = new HashSet<Type>();
        foreach (var assembly in assemblies.Where(assembly => assembly is not null).Distinct())
        {
            foreach (var type in GetLoadableTypes(assembly, logger))
            {
                if (!seenTypes.Add(type)
                    || type is not { IsClass: true, IsAbstract: false }
                    || type.ContainsGenericParameters)
                {
                    continue;
                }

                var configurationType = CreateConfigurationType(type, logger, warnWhenMissingAttribute: false);
                if (configurationType is not null)
                {
                    result.Add(configurationType);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Discovers and binds all Monica configuration types from the supplied assemblies.
    /// </summary>
    /// <param name="configuration">The host configuration available during application builder setup.</param>
    /// <param name="assemblies">Assemblies that should be scanned for Monica configuration types.</param>
    /// <param name="logger">Optional logger used to emit warnings for missing sections and binding failures.</param>
    /// <returns>The successfully created bootstrap binding results.</returns>
    public static IReadOnlyList<MonicaBootstrapConfigurationBinding> GetMonicaBootstrapConfigurations(
        this IConfiguration configuration,
        IEnumerable<Assembly> assemblies,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(assemblies);

        return assemblies
            .DiscoverMonicaBootstrapConfigurationTypes(logger)
            .Select(configurationType => TryBindConfiguration(configuration, configurationType, logger))
            .Where(binding => binding is not null)
            .Cast<MonicaBootstrapConfigurationBinding>()
            .ToArray();
    }

    private static MonicaBootstrapConfigurationType? CreateConfigurationType(
        Type type,
        ILogger? logger,
        bool warnWhenMissingAttribute = true)
    {
        var attribute = type.GetCustomAttribute<ConfigurationAttribute>(inherit: false);
        if (attribute is null)
        {
            if (warnWhenMissingAttribute)
            {
                logger?.LogWarning(
                    "Type '{OptionsType}' is not marked with {AttributeType}. Monica bootstrap binding returned CLR defaults.",
                    type.FullName ?? type.Name,
                    nameof(ConfigurationAttribute));
            }

            return null;
        }

        var definitionKey = attribute.DefinitionKey ?? type.FullName ?? type.Name;
        return new MonicaBootstrapConfigurationType
        {
            ClrType = type,
            DefinitionKey = definitionKey,
            SectionPath = attribute.SectionPath ?? definitionKey.Replace('.', ':'),
            DisplayName = attribute.DisplayName ?? type.Name,
            OwnerModule = attribute.OwnerModule,
            Category = attribute.Category,
            Description = attribute.Description
        };
    }

    private static MonicaBootstrapConfigurationBinding? TryBindConfiguration(
        IConfiguration configuration,
        MonicaBootstrapConfigurationType configurationType,
        ILogger? logger)
    {
        var fallbackValue = CreateDefaultInstance(configurationType.ClrType, logger);
        return fallbackValue is null
            ? null
            : BindConfiguration(configuration, configurationType, () => fallbackValue, logger);
    }

    private static MonicaBootstrapConfigurationBinding BindConfiguration(
        IConfiguration configuration,
        MonicaBootstrapConfigurationType configurationType,
        Func<object> fallbackFactory,
        ILogger? logger)
    {
        var section = configuration.GetSection(configurationType.SectionPath);
        if (!section.Exists())
        {
            logger?.LogWarning(
                "Configuration section '{SectionPath}' for Monica bootstrap options '{OptionsType}' was not found. CLR defaults are used.",
                configurationType.SectionPath,
                configurationType.ClrType.FullName ?? configurationType.ClrType.Name);

            return new MonicaBootstrapConfigurationBinding
            {
                ConfigurationType = configurationType,
                Value = fallbackFactory(),
                UsedClrDefaults = true
            };
        }

        try
        {
            var value = section.Get(configurationType.ClrType);
            if (value is not null)
            {
                return new MonicaBootstrapConfigurationBinding
                {
                    ConfigurationType = configurationType,
                    Value = value,
                    UsedClrDefaults = false
                };
            }

            logger?.LogWarning(
                "Configuration section '{SectionPath}' for Monica bootstrap options '{OptionsType}' did not produce a value. CLR defaults are used.",
                configurationType.SectionPath,
                configurationType.ClrType.FullName ?? configurationType.ClrType.Name);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Failed to bind configuration section '{SectionPath}' to Monica bootstrap options '{OptionsType}'. CLR defaults are used.",
                configurationType.SectionPath,
                configurationType.ClrType.FullName ?? configurationType.ClrType.Name);
        }

        return new MonicaBootstrapConfigurationBinding
        {
            ConfigurationType = configurationType,
            Value = fallbackFactory(),
            UsedClrDefaults = true
        };
    }

    private static object? CreateDefaultInstance(Type type, ILogger? logger)
    {
        try
        {
            return Activator.CreateInstance(type);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Failed to create CLR defaults for Monica bootstrap options '{OptionsType}'. The options type is skipped.",
                type.FullName ?? type.Name);
            return null;
        }
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly, ILogger? logger)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            logger?.LogWarning(
                ex,
                "Assembly '{AssemblyName}' could not be fully scanned for Monica bootstrap configuration types. Loaded types will still be used.",
                assembly.FullName);
            return ex.Types.OfType<Type>();
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Assembly '{AssemblyName}' could not be scanned for Monica bootstrap configuration types.",
                assembly.FullName);
            return [];
        }
    }
}
