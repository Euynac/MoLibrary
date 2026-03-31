using Monica.Core.Extensions;
using Microsoft.Extensions.Logging;
using Monica.Profiling.MemoryDiagnostics.Services;
using Monica.Core.Results;
using Monica.Profiling.MemoryDiagnostics.Models;

namespace Monica.Profiling.MemoryDiagnostics.Facades;

/// <summary>
/// Provides result-envelope entry points for memory snapshots, GC details, and dump creation.
/// </summary>
public sealed class MemoryDiagnosticsFacade
{
    private readonly MemoryDiagnosticsService _memoryDiagnosticsService;
    private readonly ILogger<MemoryDiagnosticsFacade> _logger;

    internal MemoryDiagnosticsFacade(
        MemoryDiagnosticsService memoryDiagnosticsService,
        ILogger<MemoryDiagnosticsFacade> logger)
    {
        _memoryDiagnosticsService = memoryDiagnosticsService;
        _logger = logger;
    }

    public Res<MemorySnapshot> GetCurrentSnapshot()
    {
        try
        {
            return Res.Ok(_memoryDiagnosticsService.CaptureSnapshot());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture the current memory snapshot.");
            return Res.Fail($"Failed to capture memory snapshot: {ex.GetMessageRecursively()}");
        }
    }

    public Res<GcDetails> GetGcDetails(GCKind kind = GCKind.Any)
    {
        try
        {
            return Res.Ok(_memoryDiagnosticsService.CaptureGcDetails(kind));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture GC details.");
            return Res.Fail($"Failed to capture GC details: {ex.GetMessageRecursively()}");
        }
    }

    public Res ForceGarbageCollection(int generation = -1, bool blocking = true, bool compacting = false)
    {
        try
        {
            _memoryDiagnosticsService.ForceGarbageCollection(generation, blocking, compacting);
            return Res.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to force garbage collection.");
            return Res.Fail($"GC execution failed: {ex.GetMessageRecursively()}");
        }
    }

    public async Task<Res<string>> TriggerGcDumpAsync(string? outputPath = null)
    {
        try
        {
            return Res.Ok(await _memoryDiagnosticsService.CreateGcDumpAsync(outputPath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create GC dump.");
            return Res.Fail($"Failed to create GC dump: {ex.GetMessageRecursively()}", ResStatus.BadRequest);
        }
    }
}
