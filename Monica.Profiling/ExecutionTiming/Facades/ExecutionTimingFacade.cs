using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.Core.Results;
using Monica.Profiling.ExecutionTiming.Models;
using Monica.Profiling.ExecutionTiming.Services;

namespace Monica.Profiling.ExecutionTiming.Facades;

/// <summary>
/// Provides result-envelope entry points for execution-timing diagnostics used by APIs and UI pages.
/// </summary>
public sealed class ExecutionTimingFacade
{
    private readonly ExecutionTimingService _executionTimingService;
    private readonly ILogger<ExecutionTimingFacade> _logger;

    internal ExecutionTimingFacade(
        ExecutionTimingService executionTimingService,
        ILogger<ExecutionTimingFacade> logger)
    {
        _executionTimingService = executionTimingService;
        _logger = logger;
    }

    public Res<IReadOnlyList<ExecutionTimingStatistics>> GetStatistics()
    {
        try
        {
            return Res.Ok(_executionTimingService.GetStatistics());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load execution timing statistics.");
            return Res.Fail($"Failed to load execution timing statistics: {ex.GetMessageRecursively()}");
        }
    }

    public Res<IReadOnlyList<RunningExecutionTimingInfo>> GetRunningOperations()
    {
        try
        {
            return Res.Ok(_executionTimingService.GetRunningOperations());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load running execution timing operations.");
            return Res.Fail($"Failed to load running execution timing operations: {ex.GetMessageRecursively()}");
        }
    }
}
