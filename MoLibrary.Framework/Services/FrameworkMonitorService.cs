using System.Text.Json;
using System.Text.Json.Nodes;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.GlobalJson.Interfaces;
using MoLibrary.Framework.Core.Extensions;
using MoLibrary.EventBus.Abstractions;
using MoLibrary.Framework.Core;
using MoLibrary.Framework.Core.Model;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Framework.Services;

/// <summary>
/// 框架监控服务实现
/// </summary>
public class FrameworkMonitorService(
    ILogger<FrameworkMonitorService> logger,
    IMoDistributedEventBus eventBus,
    IGlobalJsonOption jsonOption,
    IMapper mapper,
    IRequestFilter? requestFilter = null) : IFrameworkMonitorService
{
    /// <summary>
    /// 获取所有项目单元信息
    /// </summary>
    /// <returns>项目单元列表</returns>
    public async Task<Res<List<DtoProjectUnit>>> GetAllProjectUnitsAsync()
    {
        try
        {
            var units = ProjectUnitStores.GetAllUnits();
            var result = mapper.Map<List<DtoProjectUnit>>(units);
            
            // 计算每个单元被依赖的数量
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
            
            // 更新每个单元的被依赖数量
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
    /// 获取领域事件信息
    /// </summary>
    /// <returns>领域事件信息列表</returns>
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
    /// 发布领域事件
    /// </summary>
    /// <param name="eventKey">事件键</param>
    /// <param name="eventContent">事件内容</param>
    /// <returns>发布结果</returns>
    public async Task<Res> PublishDomainEventAsync(string eventKey, JsonNode eventContent)
    {
        try
        {
            if (ProjectUnitStores.GetUnit<UnitDomainEvent>(eventKey) is { } unitEvent)
            {
                var json = eventContent.ToString();
                var eventToPublish = JsonSerializer.Deserialize(json, unitEvent.Type, jsonOption.GlobalOptions)!;
                
                await eventBus.PublishAsync(unitEvent.Type, eventToPublish);
                
                return Res.Ok(eventToPublish)
                          .AppendMsg($"已发布{eventKey}信息");
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
    /// 管理请求过滤
    /// </summary>
    /// <param name="urls">URL列表</param>
    /// <param name="disable">是否禁用</param>
    /// <returns>被禁用的URL列表</returns>
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
    /// 获取枚举信息
    /// </summary>
    /// <param name="name">枚举名称</param>
    /// <returns>枚举信息</returns>
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
}