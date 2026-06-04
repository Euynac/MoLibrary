using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Monica.Configuration.Annotations;
using Monica.Core.Logging;

namespace Monica.Configuration.Bootstrap;

/// <summary>
/// Provides bootstrap-time helpers for reading Monica <see cref="ConfigurationAttribute"/> options before
/// the Monica effective-value store has been activated.
/// </summary>
public static class MonicaConfigurationBootstrapExtensions
{
    private static readonly ILogger Logger = LogManager.For(typeof(MonicaConfigurationBootstrapExtensions));

    /// <summary>
    /// Binds a Monica configuration options type from the host bootstrap <see cref="IConfiguration"/>.
    /// </summary>
    /// <typeparam name="TOptions">The options type marked with <see cref="ConfigurationAttribute"/>.</typeparam>
    /// <param name="configuration">The host configuration available during application builder setup.</param>
    /// <returns>
    /// The bound options object, or a CLR default instance when the section is unavailable or cannot be bound.
    /// </returns>
    /// <remarks>
    /// This helper is intended for bootstrap settings such as database connectivity that must be available before
    /// Monica-managed configuration is loaded. Runtime-managed settings should still flow through Monica.Configuration.
    /// </remarks>
    public static TOptions GetMonicaBootstrapConfiguration<TOptions>(this IConfiguration configuration)
        where TOptions : class, new()
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var sectionPath = ResolveSectionPath(typeof(TOptions));
        return sectionPath is null
            ? new TOptions()
            : BindConfiguration<TOptions>(configuration, sectionPath);
    }

    private static string? ResolveSectionPath(Type optionsType)
    {
        var attribute = optionsType.GetCustomAttribute<ConfigurationAttribute>(inherit: false);
        if (attribute is null)
        {
            Logger.LogWarning(
                "Type '{OptionsType}' is not marked with {AttributeType}. Monica bootstrap binding returned CLR defaults.",
                optionsType.FullName ?? optionsType.Name,
                nameof(ConfigurationAttribute));

            return null;
        }

        var definitionKey = attribute.DefinitionKey ?? optionsType.FullName ?? optionsType.Name;
        return attribute.SectionPath ?? definitionKey.Replace('.', ':');
    }

    private static TOptions BindConfiguration<TOptions>(IConfiguration configuration, string sectionPath)
        where TOptions : class, new()
    {
        var section = configuration.GetSection(sectionPath);
        if (!section.Exists())
        {
            Logger.LogWarning(
                "Configuration section '{SectionPath}' for Monica bootstrap options '{OptionsType}' was not found. CLR defaults are used.",
                sectionPath,
                typeof(TOptions).FullName ?? typeof(TOptions).Name);

            return new TOptions();
        }

        try
        {
            var value = section.Get<TOptions>();
            if (value is not null)
            {
                return value;
            }

            Logger.LogWarning(
                "Configuration section '{SectionPath}' for Monica bootstrap options '{OptionsType}' did not produce a value. CLR defaults are used.",
                sectionPath,
                typeof(TOptions).FullName ?? typeof(TOptions).Name);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(
                ex,
                "Failed to bind configuration section '{SectionPath}' to Monica bootstrap options '{OptionsType}'. CLR defaults are used.",
                sectionPath,
                typeof(TOptions).FullName ?? typeof(TOptions).Name);
        }

        return new TOptions();
    }
}
