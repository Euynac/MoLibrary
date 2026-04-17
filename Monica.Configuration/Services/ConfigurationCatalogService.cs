using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;
using Monica.Configuration.Models.Internal;
using Monica.Configuration.Providers.JsonFile;
using Monica.Configuration.Services.Support;
using Monica.Tool.Extensions;

namespace Monica.Configuration.Services;

public class ConfigurationCatalogService(IServiceProvider serviceProvider, IConfigurationProjectCatalog catalog) : IConfigurationCatalog
{
    public IEnumerable<ConfigurationRegistration> GetConfigCards()
    {
        using var scope = serviceProvider.CreateScope();
        var cards = ConfigurationRegistration.Cards.Values;
        foreach (var card in cards)
        {
            card.Configuration.SetOptionValue(ConfigurationTypeAccessor.GetConfig(card.Configuration.ConfigType, scope.ServiceProvider));
            yield return card;
        }
    }

    public List<ConfigurationDomainGroup> GetConfigs(bool onlyCurDomain = false)
    {
        var result = new Dictionary<string, ConfigurationDomainGroup>();
        foreach (var group in GetConfigCards().GroupBy(p => p.Configuration.FromProjectName))
        {
            var cards = group.ToList();
            var tmpCard = cards.FirstOrDefault();
            if (tmpCard == null) continue;

            // Filter by domain
            if (onlyCurDomain && !catalog.IsCurrentDomain(tmpCard.Configuration.FromProjectName))
            {
                continue;
            }

            // Get domain info
            var domainName = catalog.GetDomainName(tmpCard.Configuration.FromProjectName);
            var domainTitle = catalog.GetDomainTitle(domainName);

            // Create domain group if not exists
            if (!result.ContainsKey(domainName))
            {
                var domainConfig = new ConfigurationDomainGroup()
                {
                    Children = [],
                    Name = domainName,
                    Title = domainTitle
                };
                result.Add(domainConfig.Name, domainConfig);
            }

            var config = result[domainName];
            var serviceConfig = new ConfigurationServiceGroup()
            {
                AppId = catalog.CurrentAppId,  // Use current service's AppId
                Name = tmpCard.Configuration.FromProjectName,
                Title = catalog.GetProjectDisplayName(tmpCard.Configuration.FromProjectName),
                Children = cards.Select(c => new ConfigurationSnapshot()
                {
                    Name = c.Key,
                    Type = c.Configuration.Info.Type,
                    Desc = c.Description,
                    Title = c.Title,
                    Version = c.Version,
                    Items = c.Configuration.OptionItems.Select(i => i.ToDtoOptionItem(c.Configuration)).ToList(),
                }).ToList()
            };
            config.Children.Add(serviceConfig);
        }

        return [.. result.Values];
    }
   
}

public static class ConfigurationSnapshotMappingExtensions
{
    public static ConfigurationOptionSnapshot ToDtoOptionItem(this OptionItem i, ConfigurationDescriptor c)
    {

        var dto = new ConfigurationOptionSnapshot
        {
            Desc = i.Info?.Description,
            IsOffline = i.Info?._IsOffline ?? c.Info._IsOffline ?? false,
            IsSensitive = i.IsSensitive,
            HasStoredValue = i.HasStoredValue,
            Name = i.Name,
            Title = i.Title,
            Value = i.IsSensitive ? null : ConvertValueForDto(i),
            Type = i.BasicType,
            SpecialType = i.SpecialType,
            IsNullable = i.PropertyInfo.IsMarkedAsNullable(),
            SubStructure = ToDtoConfig(i.SubConfigInfo),
            Key = i.Key,
            RegexPattern = i.ValidateRegexPattern,
            Source = i.Source,
            Provider = i.Provider,
            SourceList = i.SourceList.Select((source, index) => new ConfigurationSourceEntry
            {
                Provider = source.Value,
                SourceInfo = source.Key,
                IsActive = index == i.SourceList.Count - 1
            }).ToList()
        };

        return dto;
    }

    private static object? ConvertValueForDto(OptionItem i)
    {
        return JsonFileConventions.ToJsonElement(i.Value);
    }

    public static ConfigurationSnapshot? ToDtoConfig(this ConfigurationDescriptor? c)
    {
        if (c == null) return null;
        var dto = new ConfigurationSnapshot()
        {
            Name = c.Name,
            Type = c.Info.Type,
            Desc = c.Info.Description,
            Title = c.Info.Title ?? c.Name,
            Version = c.Version,
            Items = c.OptionItems.Select(i => ToDtoOptionItem(i, c)).ToList()
        };
        return dto;
    }
}
