using System.Globalization;
using Microsoft.JSInterop;

namespace Monica.JobScheduler.UI.UIJobScheduler.Support;

/// <summary>
/// Owns one lazily imported Cron-description module and serializes invocation with teardown.
/// </summary>
/// <remarks>
/// Imports are always observed to completion. If disposal wins while an import is pending, the late module
/// reference is released instead of being published to the component.
/// </remarks>
internal sealed class CronDescriptionSession(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private const string MODULE_PATH = "./_content/Monica.JobScheduler.UI/js/cron-descriptions.js";
    private const string FALLBACK_CULTURE_NAME = "en-US";

    /// <summary>
    /// Resolves the culture name sent with Cron-description requests. Hosts running under invariant globalization
    /// expose an empty culture name, so they fall back to English instead of failing the description load.
    /// </summary>
    internal static string ResolveCultureName()
    {
        var cultureName = CultureInfo.CurrentUICulture.Name;
        return string.IsNullOrWhiteSpace(cultureName) ? FALLBACK_CULTURE_NAME : cultureName;
    }

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _disposeSync = new();
    private IJSObjectReference? _module;
    private Task<IJSObjectReference>? _importTask;
    private Task? _disposeTask;
    private volatile bool _disposed;

    internal async ValueTask<IReadOnlyList<CronDescriptionResult>> DescribeAsync(
        IReadOnlyList<CronDescriptionRequest> requests,
        string cultureName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var module = await GetOrImportModuleAsync(cancellationToken);
            return await module.InvokeAsync<CronDescriptionResult[]?>(
                "describeCronExpressions",
                cancellationToken,
                [requests, cultureName]) ?? [];
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_disposeSync)
        {
            return new ValueTask(_disposeTask ??= DisposeCoreAsync());
        }
    }

    private async ValueTask<IJSObjectReference> GetOrImportModuleAsync(CancellationToken cancellationToken)
    {
        if (_module is not null)
        {
            return _module;
        }

        var importTask = _importTask ??= ImportModuleAsync();
        try
        {
            var importedModule = await importTask.WaitAsync(cancellationToken);
            if (_disposed)
            {
                _importTask = null;
                await DisposeReferenceAsync(importedModule);
                throw new ObjectDisposedException(nameof(CronDescriptionSession));
            }

            _module = importedModule;
            _importTask = null;
            return importedModule;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The import remains owned by the session and will be observed by a future caller or disposal.
            throw;
        }
        catch when (importTask.IsCompleted && ReferenceEquals(_importTask, importTask))
        {
            _importTask = null;
            throw;
        }
    }

    private async Task<IJSObjectReference> ImportModuleAsync()
    {
        var module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", [MODULE_PATH]);
        return module
               ?? throw new InvalidOperationException("The Cron-description module import returned no reference.");
    }

    private async Task DisposeCoreAsync()
    {
        _disposed = true;
        await _gate.WaitAsync();
        IJSObjectReference? module;
        Task<IJSObjectReference>? importTask;
        try
        {
            module = _module;
            _module = null;
            importTask = _importTask;
            _importTask = null;
        }
        finally
        {
            _gate.Release();
        }

        await DisposeReferenceAsync(module);
        if (importTask is not null)
        {
            try
            {
                await DisposeReferenceAsync(await importTask);
            }
            catch (JSDisconnectedException)
            {
                // Circuit loss ended the pending import and released its JavaScript references.
            }
            catch (JSException)
            {
                // A failed abandoned import has been observed; there is no module reference to release.
            }
            catch (OperationCanceledException)
            {
                // Circuit teardown or interop timeout cancelled the abandoned import before it produced a module.
            }
        }
    }

    private static async ValueTask DisposeReferenceAsync(IJSObjectReference? module)
    {
        if (module is null)
        {
            return;
        }

        try
        {
            await module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The disconnected circuit already released its JavaScript references.
        }
    }
}

/// <summary>
/// Identifies one visible Cron expression for a batched description request.
/// </summary>
internal sealed record CronDescriptionRequest(string Key, string Expression);

/// <summary>
/// Carries one localized Cron description or its parser error back to the owning component.
/// </summary>
internal sealed record CronDescriptionResult(string Key, string? Description, string? Error);
