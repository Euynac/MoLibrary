using System.Diagnostics.CodeAnalysis;
using BuildingBlocksPlatform.Configuration.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.Memory;


namespace BuildingBlocksPlatform.Configuration.Model;

/// <summary>
/// 已注册的热配置类信息卡片，记录各种相关配置信息
/// </summary>
public class MoConfigurationCard
{
    internal MoConfiguration Configuration { get; }
    internal MoConfigurationCard(Type configType)
    {
        Configuration = new MoConfiguration(configType);
    }

    /// <summary>
    /// 使用配置节点名称作为Key。未配置配置节点时，使用配置类名作为配置节点。设置为孤立配置项时，则以配置类名作为Key
    /// </summary>
    public string Key => SectionName ?? Configuration.Name;

    /// <summary>
    /// <inheritdoc cref="ConfigurationAttribute.Title"/>
    /// </summary>
    public string Title => Configuration.Info?.Title ?? Configuration.Name;

    /// <summary>
    /// 配置版本
    /// </summary>
    public string Version => Configuration.Version;

    /// <summary>
    /// 配置类所在的项目名
    /// </summary>
    public required string FromProjectName { get; set; }



    /// <summary>
    /// <inheritdoc cref="ConfigurationAttribute.Description"/>
    /// </summary>
    public string? Description => Configuration.Info?.Description;

    /// <summary>
    /// <inheritdoc cref="ConfigurationAttribute.Section"/>
    /// </summary>
    public string? SectionName => Configuration.Info?.Section;

  

    /// <summary>
    /// 配置卡片池
    /// </summary>
    public static Dictionary<string, MoConfigurationCard> Cards { get; } = [];
    /// <summary>
    /// 配置卡注册
    /// </summary>
    public static void Register(MoConfigurationCard card)
    {
        if (!Cards.TryAdd(card.Key, card))
        {
            throw new InvalidOperationException($"Hot Configuration card {card.Key} already exists.");
        }
    }
    /// <summary>
    /// 配置卡注销
    /// </summary>
    public static void UnRegister(MoConfigurationCard card)
    {
        Cards.Remove(card.Key);
    }

    private static Dictionary<string, OptionItem> OptionsWithoutSectionName = [];

    internal static bool TryGetConfig(string key, [NotNullWhen(true)]out MoConfiguration? config)
    {
        config = null;
        if (Cards.TryGetValue(key, out var card))
        {
            config = card.Configuration;
        }

        return config != null;
    }

    internal static bool TryGetOptionItem(string key,[NotNullWhen(true)] out OptionItem? option)
    {
        option = null;
        var node = key.Split(":").FirstOrDefault();
        if (node == null) return false;
        if (TryGetConfig(node, out var card))
        {
            option = card.OptionItems.FirstOrDefault(p => p.Key.Equals(key));
        }

        if (option == null && OptionsWithoutSectionName.TryGetValue(key, out option))
        {
            return true;
        }

        return option != null;
    }

    /// <summary>
    /// 刷新配置类来源
    /// </summary>
    internal static void RefreshProviders()
    {
        OptionsWithoutSectionName = Cards.Where(p => p.Value.SectionName == null)
            .SelectMany(p => p.Value.Configuration.OptionItems).ToDictionary(p => p.Name, p => p);

        var source = MoConfigurationManager.AppConfiguration;

        foreach (var provider in ((IConfigurationRoot) source).Providers)
        {
            switch (provider)
            {
                case JsonConfigurationProvider jsonProvider:
                {
                    var relativePath = jsonProvider.Source.Path;
                    if(relativePath == null) continue;
              
                    var absolutePath = jsonProvider.Source.FileProvider?.GetFileInfo(relativePath).PhysicalPath;
                    SetProvider(provider, absolutePath);
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
                    SetProvider(provider, provider.ToString());
                    break;
                }
            }
        }

        return;

        static void SetProvider(IConfigurationProvider provider, string? sourceInfo)
        {
            foreach (var key in provider.GetChildKeys([], null).Distinct())
            {
                if (Cards.TryGetValue(key, out var card))
                {
                    card.Configuration.SetOptionSource(provider, sourceInfo);
                }
                else if (OptionsWithoutSectionName.TryGetValue(key, out var option))
                {
                    option.SetSource(provider, sourceInfo);
                }
            }
        }
    }

    public override string ToString()
    {
        return $"{Title}({FromProjectName}-{Key})";
    }
}