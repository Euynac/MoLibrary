using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.Framework.ProjectUnits.Models;
using Monica.Framework.ProjectUnits.Services;

namespace Monica.Framework.ProjectUnits.Facades;

/// <summary>
/// Host-facing facade for project-unit inspection and diagnostics.
/// </summary>
public class ProjectUnitsFacade(
    ILogger<ProjectUnitsFacade> logger,
    ProjectUnitCatalogService catalogService)
{
    /// <summary>
    /// Gets every discovered project unit.
    /// </summary>
    public Task<Res<List<DtoProjectUnit>>> GetAllProjectUnitsAsync()
    {
        try
        {
            return Task.FromResult(Res.Ok<List<DtoProjectUnit>>(catalogService.GetAllProjectUnits()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load project units.");
            return Task.FromResult<Res<List<DtoProjectUnit>>>($"获取项目单元信息失败: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets discovered domain-event metadata.
    /// </summary>
    public Task<Res<List<DtoDomainEventInfo>>> GetDomainEventsAsync()
    {
        try
        {
            return Task.FromResult(Res.Ok<List<DtoDomainEventInfo>>(catalogService.GetDomainEvents()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load domain events.");
            return Task.FromResult<Res<List<DtoDomainEventInfo>>>($"获取领域事件信息失败: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Publishes a domain event payload.
    /// </summary>
    public async Task<Res> PublishDomainEventAsync(string eventKey, JsonNode eventContent)
    {
        try
        {
            var eventToPublish = await catalogService.PublishDomainEventAsync(eventKey, eventContent);
            return Res.Ok(eventToPublish).AppendMessage($"已发布{eventKey}信息");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish domain event {EventKey}.", eventKey);
            return Res.Fail($"发布领域事件失败: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Reads and updates the request-filter state.
    /// </summary>
    public Task<Res<List<string>>> ManageRequestFilterAsync(List<string>? urls, bool? disable)
    {
        try
        {
            return Task.FromResult(Res.Ok<List<string>>(catalogService.ManageRequestFilter(urls, disable)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to manage request filters.");
            return Task.FromResult<Res<List<string>>>($"管理请求过滤失败: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets enum metadata.
    /// </summary>
    public Task<Res<List<DtoAssemblyEnumInfo>>> GetEnumInfoAsync(string? name = null)
    {
        try
        {
            return Task.FromResult(Res.Ok<List<DtoAssemblyEnumInfo>>(catalogService.GetEnumInfo(name)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load enum metadata.");
            return Task.FromResult<Res<List<DtoAssemblyEnumInfo>>>($"获取枚举信息失败: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets the original project-unit model for detail inspection.
    /// </summary>
    public Res<ProjectUnit?> GetProjectUnitByKey(string key)
    {
        try
        {
            return Res.Ok<ProjectUnit?>(catalogService.GetProjectUnitByKey(key));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve project unit {ProjectUnitKey}.", key);
            return Res.Fail($"获取项目单元失败: {ex.GetMessageRecursively()}");
        }
    }
}
