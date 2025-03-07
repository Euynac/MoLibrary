using BuildingBlocksPlatform.Configuration.Model;
using BuildingBlocksPlatform.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.Configuration.Interfaces;

public class MoConfigurationCardManager(IServiceProvider serviceProvider, IMoConfigurationServiceInfo info) : IMoConfigurationCardManager
{
    public IEnumerable<MoConfigurationCard> GetHotConfigCards()
    {
        using var scope = serviceProvider.CreateScope();
        var cards = MoConfigurationCard.Cards.Values;
        foreach (var card in cards)
        {
            card.Configuration.SetOptionValue(UtilsConfiguration.GetConfig(card.Configuration.ConfigType, scope.ServiceProvider));
            yield return card;
        }
    }

    public List<DtoDomainConfigs> GetDomainConfigs(bool? onlyCurDomain = null)
    {
        var result = new Dictionary<string, DtoDomainConfigs>();
        foreach (var group in GetHotConfigCards().GroupBy(p => p.FromProjectName))
        {
            var cards = group.ToList();
            var tmpCard = cards.FirstOrDefault();
            if (tmpCard == null) continue;
            if (onlyCurDomain is true && !info.IsCurrentDomain(tmpCard.FromProjectName))
            {
                continue;
            }
            var serviceInfo = info.GetServiceInfo(tmpCard.FromProjectName);
            var domainName = serviceInfo.DomainName;
            var domainTitle = serviceInfo.DomainTitle;
            if (!result.ContainsKey(domainName))
            {
                var domainConfig = new DtoDomainConfigs()
                {
                    Children = [],
                    Name = domainName,
                    Title = domainTitle
                };
                result.Add(domainConfig.Name, domainConfig);
            }
            var config = result[domainName];
            var serviceConfig = new DtoServiceConfigs()
            {
                AppId = serviceInfo.AppId,
                Name = serviceInfo.ServiceName,
                Title = serviceInfo.ServiceTitle,
                Children = cards.Select(c => new DtoConfig()
                {
                    Name = c.Key,
                    Type = c.Configuration.Info.Type,
                    AppId = serviceInfo.AppId,
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
            Value = i.Value,
            Type = i.BasicType,
            SpecialType = i.SpecialType,
            IsNullable = i.PropertyInfo.IsMarkedAsNullable(),
            SubStructure = ToDtoConfig(i.SubConfigInfo),
            Key = i.Key,
            RegexPattern = i.ValidateRegexPattern,
            Source = i.Source,
            Provider = i.Provider
        };

        return dto;
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