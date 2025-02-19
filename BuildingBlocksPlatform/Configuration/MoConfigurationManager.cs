using System.Text;
using BuildingBlocksPlatform.Configuration.Model;
using Koubot.Tool.General;
using Microsoft.Extensions.Configuration;

namespace BuildingBlocksPlatform.Configuration;

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
    private static MoConfigurationSetting? _setting;

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
    internal static ILogger Logger =>
        Setting.Logger ??= LoggerFactory.Create(builder =>
        {
            builder.AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddFilter("MoConfiguration", LogLevel.Debug)
                .AddConsole();
        }).CreateLogger("MoConfiguration");

    /// <summary>
    /// Gets or sets the configuration settings.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the settings are not initialized.</exception>
    internal static MoConfigurationSetting Setting
    {
        get => _setting ?? throw new InvalidOperationException(
            $"Setting is not initialized in {typeof(MoConfigurationManager)}. Please register MoConfiguration first");
        set => _setting = value;
    }

    #region 调试

    /// <summary>
    /// Gets the configuration providers information for the specified configuration.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <returns>A string containing the configuration providers information.</returns>
    public static string GetProviders(this IConfiguration configuration)
    {
        var sb = new StringBuilder();
        foreach (var provider in ((IConfigurationRoot)configuration).Providers)
        {
            sb.AppendLine($"Provider: {provider.ToString()}");

            //显示所有的键值对
            foreach (var key in GetConfigurationFullKeys(provider, null))
            {
                provider.TryGet(key, out var value);
                sb.AppendLine($"- {key} : {value}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the full keys for the specified configuration provider.
    /// </summary>
    /// <param name="provider">The configuration provider.</param>
    /// <param name="parentPath">The parent path.</param>
    /// <returns>A list of full keys.</returns>
    private static IEnumerable<string> GetConfigurationFullKeys(IConfigurationProvider provider, string? parentPath)
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
    /// Gets the application configuration providers information.
    /// </summary>
    /// <returns>A string containing the application configuration providers information.</returns>
    public static string GetProviders() => GetProviders(AppConfiguration);

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

    #region 生成配置文件

    /// <summary>
    /// Generates configuration files for the specified configuration card.
    /// </summary>
    /// <param name="card">The configuration card.</param>
    internal static void GenConfigurationFiles(MoConfigurationCard card)
    {
        if (!Setting.GenerateFileForEachOption) return;
        var filename = $"{card.FromProjectName}.{card.Configuration.Name}.json";
        if (Setting.GenerateOptionFileParentDirectory is { } parent && !string.IsNullOrWhiteSpace(parent))
        {
            filename = Path.Combine(parent, filename);
        }

        var path = GeneralExtensions.GetRelativePathInRunningPath(filename);
        if (!File.Exists(path))
        {
            var directory = FileTool.GetDirectoryPath(path)!;
            Directory.CreateDirectory(directory);
            var contents = card.GetDefaultFileContents();
            File.WriteAllText(path, contents, Encoding.UTF8);
        }

        ((ConfigurationManager)AppConfiguration).AddJsonFile(path, false, true);
    }

    #endregion
}