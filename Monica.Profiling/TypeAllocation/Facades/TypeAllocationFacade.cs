using Monica.Core.Extensions;
using Microsoft.Extensions.Logging;
using Monica.Profiling.TypeAllocation.Services;
using Monica.Core.Results;
using Monica.Profiling.TypeAllocation.Models;

namespace Monica.Profiling.TypeAllocation.Facades;

/// <summary>
/// Provides result-envelope entry points for type allocation tracking and heap snapshots.
/// </summary>
public sealed class TypeAllocationFacade
{
    private readonly TypeAllocationTrackingService _typeAllocationTrackingService;
    private readonly ILogger<TypeAllocationFacade> _logger;

    internal TypeAllocationFacade(
        TypeAllocationTrackingService typeAllocationTrackingService,
        ILogger<TypeAllocationFacade> logger)
    {
        _typeAllocationTrackingService = typeAllocationTrackingService;
        _logger = logger;
    }

    public bool IsCollecting => _typeAllocationTrackingService.IsCollecting;

    public AllocationSamplingMode CurrentMode => _typeAllocationTrackingService.CurrentMode;

    public Res<TypeAllocationSnapshot> GetCurrentSnapshot()
    {
        try
        {
            return Res.Ok(_typeAllocationTrackingService.GetCurrentSnapshot());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load the current type allocation snapshot.");
            return Res.Fail($"Failed to load type allocation snapshot: {ex.GetMessageRecursively()}");
        }
    }

    public Res<IReadOnlyList<TypeAllocationSnapshot>> GetHistorySnapshots()
    {
        try
        {
            return Res.Ok(_typeAllocationTrackingService.GetHistorySnapshots());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load type allocation history.");
            return Res.Fail($"Failed to load type allocation history: {ex.GetMessageRecursively()}");
        }
    }

    public Res StartCollection(AllocationSamplingMode mode = AllocationSamplingMode.Low)
    {
        try
        {
            _typeAllocationTrackingService.StartCollection(mode);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start type allocation collection.");
            return Res.Fail(ex.GetMessageRecursively());
        }
    }

    public Res StopCollection()
    {
        try
        {
            _typeAllocationTrackingService.StopCollection();
            return Res.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop type allocation collection.");
            return Res.Fail(ex.GetMessageRecursively());
        }
    }

    public Res ResetData()
    {
        try
        {
            _typeAllocationTrackingService.ResetData();
            return Res.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset type allocation data.");
            return Res.Fail(ex.GetMessageRecursively());
        }
    }

    public Action SubscribeToUpdates(Action<TypeAllocationSnapshot> callback)
    {
        return _typeAllocationTrackingService.SubscribeToUpdates(callback);
    }

    public async Task<Res<TypeAllocationSnapshot>> TakeHeapSnapshotAsync()
    {
        try
        {
            return Res.Ok(await _typeAllocationTrackingService.TakeHeapSnapshotAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture heap snapshot.");
            return Res.Fail($"Failed to capture heap snapshot: {ex.GetMessageRecursively()}");
        }
    }
}
