using Microsoft.Extensions.DependencyInjection;
using Monica.Configuration.Model;
using Monica.Configuration.Providers;
using Monica.Tool.Extensions;

namespace Monica.Configuration.Interfaces;

public class MoConfigurationCardManager(IServiceProvider serviceProvider, IMoProjectCatalog catalog) : IMoConfigurationCardManager
{
    public IEnumerable<MoConfigurationCard> GetConfigCards()
    {
        using var scope = serviceProvider.CreateScope();
        var cards = MoConfigurationCard.Cards.Values;
        foreach (var card in cards)
        {
            card.Configuration.SetOptionValue(UtilsConfiguration.GetConfig(card.Configuration.ConfigType, scope.ServiceProvider));
            yield return card;
        }
    }

    public List<DtoDomainGroup> GetConfigs(bool onlyCurDomain = false)
    {
        var result = new Dictionary<string, DtoDomainGroup>();
        foreach (var group in GetConfigCards().GroupBy(p => p.FromProjectName))
        {
            var cards = group.ToList();
            var tmpCard = cards.FirstOrDefault();
            if (tmpCard == null) continue;

            // Filter by domain
            if (onlyCurDomain && !catalog.IsCurrentDomain(tmpCard.FromProjectName))
            {
                continue;
            }

            // Get domain info
            var domainName = catalog.GetDomainName(tmpCard.FromProjectName);
            var domainTitle = catalog.GetDomainTitle(domainName);

            // Create domain group if not exists
            if (!result.ContainsKey(domainName))
            {
                var domainConfig = new DtoDomainGroup()
                {
                    Children = [],
                    Name = domainName,
                    Title = domainTitle
                };
                result.Add(domainConfig.Name, domainConfig);
            }

            var config = result[domainName];
            var serviceConfig = new DtoServiceGroup()
            {
                AppId = catalog.CurrentAppId,  // Use current service's AppId
                Name = tmpCard.FromProjectName,
                Title = catalog.GetProjectDisplayName(tmpCard.FromProjectName),
                Children = cards.Select(c => new DtoConfig()
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

public static class MoConfigurationExtensions
{
    public static DtoOptionItem ToDtoOptionItem(this OptionItem i, MoConfiguration c)
    {

        var dto = new DtoOptionItem
        {
            Desc = i.Info?.Description,
            IsOffline = i.Info?._IsOffline ?? c.Info._IsOffline ?? false,
            Name = i.Name,
            Title = i.Title,
            Value = ConvertValueForDto(i),
            Type = i.BasicType,
            SpecialType = i.SpecialType,
            IsNullable = i.PropertyInfo.IsMarkedAsNullable(),
            SubStructure = ToDtoConfig(i.SubConfigInfo),
            Key = i.Key,
            RegexPattern = i.ValidateRegexPattern,
            Source = i.Source,
            Provider = i.Provider,
            SourceList = i.SourceList.Select((source, index) => new DtoConfigSource
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
        return JsonFileProviderConventions.ToJsonElement(i.Value);
    }

    public static DtoConfig? ToDtoConfig(this MoConfiguration? c)
    {
        if (c == null) return null;
        var dto = new DtoConfig()
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