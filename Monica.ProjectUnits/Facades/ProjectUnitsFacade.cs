using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.ProjectUnits.Models;
using Monica.ProjectUnits.Services;

namespace Monica.ProjectUnits.Facades;

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
    /// <returns>A result envelope containing the project-unit projections for the current host.</returns>
    public Task<Res<List<DtoProjectUnit>>> GetAllProjectUnitsAsync()
    {
        try
        {
            return Task.FromResult(Res.Ok<List<DtoProjectUnit>>(catalogService.GetAllProjectUnits()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load project units.");
            return Task.FromResult<Res<List<DtoProjectUnit>>>($"Failed to load project units: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets discovered domain-event metadata.
    /// </summary>
    /// <returns>A result envelope containing domain-event metadata and representative payload structures.</returns>
    public Task<Res<List<DtoDomainEventInfo>>> GetDomainEventsAsync()
    {
        try
        {
            return Task.FromResult(Res.Ok<List<DtoDomainEventInfo>>(catalogService.GetDomainEvents()));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load domain events.");
            return Task.FromResult<Res<List<DtoDomainEventInfo>>>($"Failed to load domain events: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Publishes a domain event payload.
    /// </summary>
    /// <param name="eventKey">The discovered domain-event short type name.</param>
    /// <param name="eventContent">The JSON payload to deserialize as the selected event type.</param>
    /// <returns>A result envelope describing whether the event was published.</returns>
    public async Task<Res> PublishDomainEventAsync(string eventKey, JsonNode eventContent)
    {
        try
        {
            var eventToPublish = await catalogService.PublishDomainEventAsync(eventKey, eventContent);
            return Res.Ok(eventToPublish).AppendMessage($"Published domain event '{eventKey}'.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish domain event {EventKey}.", eventKey);
            return Res.Fail($"Failed to publish domain event: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Reads and updates the request-filter state.
    /// </summary>
    /// <param name="urls">Request paths to update, or <see langword="null"/> to query the current state.</param>
    /// <param name="disable"><see langword="true"/> to disable the paths; <see langword="false"/> to enable them.</param>
    /// <returns>A result envelope containing the currently disabled request paths.</returns>
    public Task<Res<List<string>>> ManageRequestFilterAsync(List<string>? urls, bool? disable)
    {
        try
        {
            return Task.FromResult(Res.Ok<List<string>>(catalogService.ManageRequestFilter(urls, disable)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to manage request filters.");
            return Task.FromResult<Res<List<string>>>($"Failed to manage request filters: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets enum metadata.
    /// </summary>
    /// <param name="name">An optional short enum type name used to filter the result.</param>
    /// <returns>A result envelope containing discovered enum metadata.</returns>
    public Task<Res<List<DtoAssemblyEnumInfo>>> GetEnumInfoAsync(string? name = null)
    {
        try
        {
            return Task.FromResult(Res.Ok<List<DtoAssemblyEnumInfo>>(catalogService.GetEnumInfo(name)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load enum metadata.");
            return Task.FromResult<Res<List<DtoAssemblyEnumInfo>>>($"Failed to load enum metadata: {ex.GetMessageRecursively()}");
        }
    }

    /// <summary>
    /// Gets the original project-unit model for detail inspection.
    /// </summary>
    /// <param name="key">The represented CLR type's full name.</param>
    /// <returns>A result envelope containing the project unit, or <see langword="null"/> when it is not found.</returns>
    public Res<ProjectUnit?> GetProjectUnitByKey(string key)
    {
        try
        {
            return Res.Ok<ProjectUnit?>(catalogService.GetProjectUnitByKey(key));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve project unit {ProjectUnitKey}.", key);
            return Res.Fail($"Failed to resolve project unit: {ex.GetMessageRecursively()}");
        }
    }
}
