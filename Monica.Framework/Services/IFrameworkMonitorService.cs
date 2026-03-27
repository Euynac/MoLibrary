using System.Text.Json.Nodes;
using Monica.Framework.Core.Model;
using Monica.Tool.Results;

namespace Monica.Framework.Services;

/// <summary>
/// Framework monitoring service interface
/// </summary>
public interface IFrameworkMonitorService
{
    /// <summary>
    /// Get all project unit information
    /// </summary>
    /// <returns>Project unit list</returns>
    Task<Res<List<DtoProjectUnit>>> GetAllProjectUnitsAsync();

    /// <summary>
    /// Get domain event information
    /// </summary>
    /// <returns>Domain event information list</returns>
    Task<Res<List<DtoDomainEventInfo>>> GetDomainEventsAsync();

    /// <summary>
    /// Publish domain events
    /// </summary>
    /// <param name="eventKey">event key</param>
    /// <param name="eventContent">Event content</param>
    /// <returns>publish results</returns>
    Task<Res> PublishDomainEventAsync(string eventKey, JsonNode eventContent);

    /// <summary>
    /// Manage request filtering
    /// </summary>
    /// <param name="urls">URL list</param>
    /// <param name="disable">Whether to disable</param>
    /// <returns>List of banned URLs</returns>
    Task<Res<List<string>>> ManageRequestFilterAsync(List<string>? urls, bool? disable);

    /// <summary>
    /// Get enumeration information
    /// </summary>
    /// <param name="name">enum name</param>
    /// <returns>Enumeration information</returns>
    Task<Res<List<DtoAssemblyEnumInfo>>> GetEnumInfoAsync(string? name = null);

    /// <summary>
    /// Get project unit by key value
    /// </summary>
    /// <param name="key">Project unit key value</param>
    /// <returns>project unit</returns>
    ProjectUnit? GetProjectUnitByKey(string key);
}