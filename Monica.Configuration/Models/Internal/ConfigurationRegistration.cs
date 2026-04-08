using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.Memory;
using Monica.Configuration.Annotations;
using Monica.Configuration.Services.Support;
using Monica.Tool.Extensions;

namespace Monica.Configuration.Models.Internal;

/// <summary>
/// Metadata card for a registered hot-configuration type.
/// </summary>
public class ConfigurationRegistration
{
    internal ConfigurationDescriptor Configuration { get; }
    internal ConfigurationRegistration(Type configType)
    {
        Configuration = new ConfigurationDescriptor(configType);
    }

    /// <summary>
    /// Uses the section name as the key.
    /// If no section is configured, falls back to the configuration type name.
    /// For isolated key-value configuration, the type name is also used as the key.
    /// </summary>
    public string Key => SectionName ?? Configuration.Name;

    /// <summary>
    /// <inheritdoc cref="ConfigurationAttribute.Title"/>
    /// </summary>
    public string Title => Configuration.Info?.Title ?? Configuration.Name;

    /// <summary>
    /// Configuration version.
    /// </summary>
    public string Version => Configuration.Version;

  

    /// <summary>
    /// <inheritdoc cref="ConfigurationAttribute.Description"/>
    /// </summary>
    public string? Description => Configuration.Info?.Description;

    /// <summary>
    /// <inheritdoc cref="ConfigurationAttribute.Section"/>
    /// </summary>
    public string? SectionName => Configuration.Info.Section;

   

    /// <summary>
    /// Configuration card registry.
    /// </summary>
    public static Dictionary<string, ConfigurationRegistration> Cards { get; } = [];
    /// <summary>
    /// Registers a configuration card.
    /// </summary>
    public static void Register(ConfigurationRegistration card)
    {
        if (!Cards.TryAdd(card.Key, card))
        {
            throw new InvalidOperationException($"Hot Configuration card {card.Key} already exists.");
        }
    }
    /// <summary>
    /// Unregisters a configuration card.
    /// </summary>
    public static void UnRegister(ConfigurationRegistration card)
    {
        Cards.Remove(card.Key);
    }

    private static Dictionary<string, OptionItem> OptionsWithoutSectionName = [];

    public static bool TryGetConfig(string key, [NotNullWhen(true)]out ConfigurationDescriptor? config)
    {
        config = null;
        if (Cards.TryGetValue(key, out var card))
        {
            config = card.Configuration;
        }

        return config != null;
    }

    public static bool TryGetOptionItem(string key,[NotNullWhen(true)] out OptionItem? option)
    {
        option = null;
        var node = key.Split(":").FirstOrDefault();
        if (node == null) return false;
        if (TryGetConfig(node, out var card))
        {
            var configKey = key.Split(":").Take(2).StringJoin(":");
            option = card.OptionItems.FirstOrDefault(p => p.Key.Equals(configKey));
        }

        if (option == null && OptionsWithoutSectionName.TryGetValue(key, out option))
        {
            return true;
        }

        return option != null;
    }

    /// <summary>
    /// Refreshes provider-source metadata for all configuration options.
    /// </summary>
    internal static void RefreshProviders()
    {
        OptionsWithoutSectionName = Cards.Where(p => p.Value.SectionName == null)
            .SelectMany(p => p.Value.Configuration.OptionItems).ToDictionary(p => p.Name, p => p);

        var source = ConfigurationRuntime.AppConfiguration;

        foreach (var provider in ((IConfigurationRoot) source).Providers)
        {
            switch (provider)
            {
                case JsonConfigurationProvider jsonProvider:
                {
                    var relativePath = jsonProvider.Source.Path;
                    if(relativePath == null) continue;
              
                    var absolutePath = jsonProvider.Source.FileProvider?.GetFileInfo(relativePath).PhysicalPath;
                    SetProvider(provider, absolutePath ?? "Failed to get absolute path");
                    break;
                }
                case MemoryConfigurationProvider memory:
                {
                    SetProvider(provider, "memory");
                    break;
                }
                case EnvironmentVariablesConfigurationProvider environmentVariables:
                {
                    SetProvider(provider, environmentVariables.ToString());
                    break;
                }
                default:
                {
                    SetProvider(provider, provider.ToString() ?? "Unknown provider");
                    break;
                }
            }
        }

        return;

        static void SetProvider(IConfigurationProvider provider, string sourceInfo)
        {
            foreach (var key in ConfigurationRuntime.GetConfigurationFullKeys(provider, null))
            {
                if (TryGetOptionItem(key, out var option))
                {
                    option.SetSource(provider, sourceInfo);
                }
            }
        }
    }

    public override string ToString()
    {
        return $"{Title}({Configuration.FromProjectName}-{Key})";
    }
}
