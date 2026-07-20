using System.Text.Json;
using System.Text.Json.Nodes;
using MapsterMapper;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.EventBus.Abstractions;
using Monica.ProjectUnits.Abstractions;
using Monica.ProjectUnits.Models;
using Monica.Tool.Extensions;

namespace Monica.ProjectUnits.Services;

/// <summary>
/// Provides the internal project-unit query and command operations used by the facade.
/// </summary>
public sealed class ProjectUnitCatalogService(
    IDistributedEventBus eventBus,
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    IMapper mapper,
    IProjectUnitCatalog catalog,
    IRequestFilter? requestFilter = null)
{
    /// <summary>
    /// Gets every discovered project unit.
    /// </summary>
    public List<DtoProjectUnit> GetAllProjectUnits()
    {
        var units = catalog.GetAllUnits();
        var result = mapper.Map<List<DtoProjectUnit>>(units);
        PopulateDependedByCounts(result);
        return result;
    }

    /// <summary>
    /// Gets discovered domain-event metadata.
    /// </summary>
    public List<DtoDomainEventInfo> GetDomainEvents()
    {
        return catalog.GetUnits<UnitDomainEvent>()
            .Select(unit => new DtoDomainEventInfo
            {
                Info = mapper.Map<DtoProjectUnit>(unit),
                Structure = unit.GetStructure()
            })
            .ToList();
    }

    /// <summary>
    /// Publishes a domain event by the discovered project-unit key.
    /// </summary>
    public async Task<object> PublishDomainEventAsync(string eventKey, JsonNode eventContent)
    {
        if (catalog.FindByName<UnitDomainEvent>(eventKey) is not { } unitEvent)
        {
            throw new KeyNotFoundException($"Project-unit metadata for domain event '{eventKey}' was not found.");
        }

        var json = eventContent.ToString();
        var eventToPublish = JsonSerializer.Deserialize(json, unitEvent.Type, jsonSerializerOptionsProvider.SerializerOptions)
            ?? throw new InvalidOperationException($"Domain event '{eventKey}' could not be deserialized.");

        await eventBus.PublishAsync(unitEvent.Type, eventToPublish);
        return eventToPublish;
    }

    /// <summary>
    /// Reads and updates the request-filter state.
    /// </summary>
    public List<string> ManageRequestFilter(List<string>? urls, bool? disable)
    {
        if (requestFilter == null)
        {
            return [];
        }

        if (urls is { } urlList && disable is { } disableFlag)
        {
            foreach (var url in urlList)
            {
                if (disableFlag)
                {
                    requestFilter.Disable(url);
                }
                else
                {
                    requestFilter.Enable(url);
                }
            }
        }

        return requestFilter.GetDisabledUrls();
    }

    /// <summary>
    /// Gets enum metadata for the whole registry or for one specific enum name.
    /// </summary>
    public List<DtoAssemblyEnumInfo> GetEnumInfo(string? name = null)
    {
        if (name == null)
        {
            return catalog.EnumTypes
                .GroupBy(item => item.Value.Assembly.FullName)
                .Select(group => new DtoAssemblyEnumInfo
                {
                    From = group.Key,
                    Enums = group.Select(item => new DtoEnumInfo
                    {
                        Name = item.Key,
                        Values = item.Value.GetEnumValues().OfType<Enum>().Select((value, index) => new DtoEnumValue
                        {
                            Index = index,
                            Name = value.ToString(),
                            Description = value.GetDescription(),
                        }).ToList()
                    }).ToList()
                })
                .ToList();
        }

        if (!catalog.EnumTypes.TryGetValue(name, out var enumType))
        {
            throw new KeyNotFoundException($"Enum type '{name}' was not found in the current host's project-unit catalog.");
        }

        return
        [
            new DtoAssemblyEnumInfo
            {
                From = enumType.Assembly.FullName,
                Enums =
                [
                    new DtoEnumInfo
                    {
                        Name = name,
                        Values = enumType.GetEnumValues().OfType<Enum>().Select((value, index) => new DtoEnumValue
                        {
                            Index = index,
                            Name = value.ToString(),
                            Description = value.GetDescription(),
                        }).ToList()
                    }
                ]
            }
        ];
    }

    /// <summary>
    /// Gets the original project-unit model by key.
    /// </summary>
    public ProjectUnit? GetProjectUnitByKey(string key)
    {
        return catalog.FindByFullName(key);
    }

    private static void PopulateDependedByCounts(List<DtoProjectUnit> units)
    {
        var dependencyCountMap = new Dictionary<string, int>();

        foreach (var unit in units)
        {
            foreach (var dependency in unit.DependencyUnits)
            {
                if (!dependencyCountMap.TryAdd(dependency.Key, 1))
                {
                    dependencyCountMap[dependency.Key]++;
                }
            }
        }

        foreach (var unit in units)
        {
            if (dependencyCountMap.TryGetValue(unit.Key, out var count))
            {
                unit.DependedByCount = count;
            }
        }
    }
}
