using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MoLibrary.Configuration.Modules;
using MoLibrary.Configuration.Model;

namespace MoLibrary.Configuration;

/// <summary>
/// Provides utility methods and properties for managing application configuration and logging.
/// </summary>
/// <remarks>
/// This static class is designed to facilitate configuration management, including retrieving
/// configuration providers, generating configuration files, and obtaining debug views of the configuration.
/// It also provides access to application-wide settings and logging.
/// </remarks>
public static class MoConfigurationManager
{
    private static IConfiguration? _appConfiguration;
    private static ModuleConfigurationOption? _setting;

    /// <summary>
    /// Gets or sets the application configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the application configuration is not initialized.</exception>
    internal static IConfiguration AppConfiguration
    {
        get
        {
            if (_appConfiguration == null)
            {
                throw new InvalidOperationException(
                    $"AppConfiguration is not initialized in {typeof(MoConfigurationManager)}.");
            }

            return _appConfiguration;
        }
        set => _appConfiguration = value;
    }

    /// <summary>
    /// Gets the logger. If the logger is not initialized, a console logger is used.
    /// </summary>
    internal static ILogger Logger => Setting.Logger;

    /// <summary>
    /// Gets or sets the configuration settings.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the settings are not initialized.</exception>
    public static ModuleConfigurationOption Setting
    {
        get => _setting ?? throw new InvalidOperationException(
            $"Setting is not initialized in {typeof(MoConfigurationManager)}. Please register MoConfiguration first");
        set => _setting = value;
    }

    /// <summary>
    /// Forces a reload of the application configuration from all providers.
    /// </summary>
    public static void Reload()
    {
        ((IConfigurationRoot)AppConfiguration).Reload();
    }

    #region 调试


    /// <summary>
    /// Gets the configuration providers information for the specified configuration as grouped strong-typed result.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>A list of grouped configuration provider information.</returns>
    public static List<DtoConfigurationProviderGroup> GetProvidersGrouped(this IConfiguration configuration)
    {
        var providers = ((IConfigurationRoot)configuration).Providers;
        var groupedProviders = new Dictionary<string, List<DtoConfigurationProvider>>();

        foreach (var provider in providers)
        {
            var providerType = provider.GetType().Name;
            var providerName = provider.ToString() ?? providerType;
            
            var configData = new Dictionary<string, string?>();
            foreach (var key in GetConfigurationFullKeys(provider, null))
            {
                provider.TryGet(key, out var value);
                configData[key] = value;
            }

            var providerDto = new DtoConfigurationProvider
            {
                Name = providerName,
                Type = providerType,
                ConfigurationData = configData
            };

            if (!groupedProviders.ContainsKey(providerType))
            {
                groupedProviders[providerType] = new List<DtoConfigurationProvider>();
            }
            groupedProviders[providerType].Add(providerDto);
        }

        return groupedProviders.Select(group => new DtoConfigurationProviderGroup
        {
            GroupName = group.Key,
            Providers = group.Value
        }).ToList();
    }

    /// <summary>
    /// Gets the full keys for the specified configuration provider.
    /// </summary>
    /// <param name="provider">The configuration provider.</param>
    /// <param name="parentPath">The parent path.</param>
    /// <returns>A list of full keys.</returns>
    public static IEnumerable<string> GetConfigurationFullKeys(IConfigurationProvider provider, string? parentPath)
    {
        var keys = new List<string>();
        var children = provider.GetChildKeys([], parentPath).Distinct().ToList();

        if (!string.IsNullOrEmpty(parentPath) && children.Count == 0)
        {
            keys.Add(parentPath);
        }
        foreach (var fullPath in children.Select(child => string.IsNullOrEmpty(parentPath) ? child : $"{parentPath}:{child}"))
        {
            keys.AddRange(GetConfigurationFullKeys(provider, fullPath));
        }
        return keys;
    }


    /// <summary>
    /// Gets the application configuration providers information as grouped strong-typed result.
    /// </summary>
    /// <returns>A list of grouped configuration provider information.</returns>
    public static List<DtoConfigurationProviderGroup> GetProvidersGrouped() => GetProvidersGrouped(AppConfiguration);

    /// <summary>
    /// Gets the debug view of the application configuration.
    /// </summary>
    /// <returns>A string containing the debug view of the application configuration.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the application configuration is not initialized.</exception>
    public static string GetDebugView() => GetDebugView(AppConfiguration);

    /// <summary>
    /// Gets the debug view of the specified configuration.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>A string containing the debug view of the specified configuration.</returns>
    public static string GetDebugView(IConfiguration configuration)
    {
        return ((IConfigurationRoot)configuration).GetDebugView();
    }

    #endregion

   
}