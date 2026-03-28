using System.Text.Json;
using System.Text.Json.Nodes;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.Framework.Core.Extensions;
using Monica.EventBus.Abstractions;
using Monica.Framework.Core;
using Monica.Framework.Core.Model;
using Monica.Tool.Extensions;
using Monica.Core.Results;

namespace Monica.Framework.Services;

/// <summary>
/// Framework monitoring service implementation
/// </summary>
public class FrameworkMonitorService(
    ILogger<FrameworkMonitorService> logger,
    IMoDistributedEventBus eventBus,
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    IMapper mapper,
    IRequestFilter? requestFilter = null) : IFrameworkMonitorService
{
    /// <summary>
    /// Get all project unit information
    /// </summary>
    /// <returns>Project unit list</returns>
    public async Task<Res<List<DtoProjectUnit>>> GetAllProjectUnitsAsync()
    {
        try
        {
            var units = ProjectUnitStores.GetAllUnits();
            var result = mapper.Map<List<DtoProjectUnit>>(units);
            
            // Calculate the number of dependencies of each unit
            var dependencyCountMap = new Dictionary<string, int>();
            foreach (var unit in result)
            {
                foreach (var dep in unit.DependencyUnits)
                {
                    if (!dependencyCountMap.ContainsKey(dep.Key))
                        dependencyCountMap[dep.Key] = 0;
                    dependencyCountMap[dep.Key]++;
                }
            }
            
            // Update the number of dependencies for each unit
            foreach (var unit in result)
            {
                if (dependencyCountMap.TryGetValue(unit.Key, out var count))
                {
                    unit.DependedByCount = count;
                }
            }
            
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取项目单元信息失败");
            return Res.Fail($"获取项目单元信息失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Get domain event information
    /// </summary>
    /// <returns>Domain event information list</returns>
    public async Task<Res<List<DtoDomainEventInfo>>> GetDomainEventsAsync()
    {
        try
        {
            var events = ProjectUnitStores.GetUnits<UnitDomainEvent>();
            var result = events.Select(p => new DtoDomainEventInfo 
            { 
                Info = mapper.Map<DtoProjectUnit>(p), 
                Structure = p.GetStructure() 
            }).ToList();
            return Res.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取领域事件信息失败");
            return Res.Fail($"获取领域事件信息失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Publish domain events
    /// </summary>
    /// <param name="eventKey">event key</param>
    /// <param name="eventContent">Event content</param>
    /// <returns>publish results</returns>
    public async Task<Res> PublishDomainEventAsync(string eventKey, JsonNode eventContent)
    {
        try
        {
            if (ProjectUnitStores.GetUnitByName<UnitDomainEvent>(eventKey) is { } unitEvent)
            {
                var json = eventContent.ToString();
                var eventToPublish = JsonSerializer.Deserialize(json, unitEvent.Type, jsonSerializerOptionsProvider.SerializerOptions)!;
                
                await eventBus.PublishAsync(unitEvent.Type, eventToPublish);
                
                return Res.Ok(eventToPublish)
                          .AppendMessage($"已发布{eventKey}信息");
            }
            return Res.Fail($"获取{eventKey}相关单元信息失败");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "发布领域事件失败，事件键: {EventKey}", eventKey);
            return Res.Fail($"发布领域事件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Manage request filtering
    /// </summary>
    /// <param name="urls">URL list</param>
    /// <param name="disable">Whether to disable</param>
    /// <returns>List of banned URLs</returns>
    public async Task<Res<List<string>>> ManageRequestFilterAsync(List<string>? urls, bool? disable)
    {
        try
        {
            if (requestFilter == null)
            {
                logger.LogWarning("请求过滤器未启用");
                return Res.Ok(new List<string>());
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

            var disabledUrls = requestFilter.GetDisabledUrls();
            return Res.Ok(disabledUrls);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "管理请求过滤失败");
            return Res.Fail($"管理请求过滤失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Get enumeration information
    /// </summary>
    /// <param name="name">enum name</param>
    /// <returns>Enumeration information</returns>
    public async Task<Res<List<DtoAssemblyEnumInfo>>> GetEnumInfoAsync(string? name = null)
    {
        try
        {
            if (name == null)
            {
                var list = ProjectUnitStores.EnumTypes.GroupBy(g => g.Value.Assembly.FullName).Select(p => new DtoAssemblyEnumInfo
                {
                    From = p.Key,
                    Enums = p.ToList().Select(e => new DtoEnumInfo
                    {
                        Name = e.Key,
                        Values = e.Value.GetEnumValues().OfType<Enum>().Select((val, i) => new DtoEnumValue
                        {
                            Index = i,
                            Name = val.ToString(),
                            Description = val.GetDescription(),
                        }).ToList()
                    }).ToList()
                }).ToList();

                return Res.Ok(list);
            }

            if (ProjectUnitStores.EnumTypes.TryGetValue(name, out var enumType))
            {
                var enumInfo = new DtoEnumInfo
                {
                    Name = name,
                    Values = enumType.GetEnumValues().OfType<Enum>().Select((val, i) => new DtoEnumValue
                    {
                        Index = i,
                        Name = val.ToString(),
                        Description = val.GetDescription(),
                    }).ToList()
                };

                var result = new List<DtoAssemblyEnumInfo>
                {
                    new()
                    {
                        From = enumType.Assembly.FullName,
                        Enums = [enumInfo]
                    }
                };

                return Res.Ok(result);
            }
            else
            {
                return Res.Fail("未找到相应的枚举类型");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取枚举信息失败");
            return Res.Fail($"获取枚举信息失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Get project unit by key value
    /// </summary>
    /// <param name="key">Project unit key value</param>
    /// <returns>project unit</returns>
    public ProjectUnit? GetProjectUnitByKey(string key)
    {
        return ProjectUnitStores.ProjectUnitsByFullName.TryGetValue(key, out var unit) ? unit : null;
    }
}
