# Page Auto-Refresh Pattern

Use this pattern for Blazor pages that need local polling or refresh ticks.

## Why

- `System.Timers.Timer.Elapsed` can fire after navigation and disposal.
- `PeriodicTimer.WaitForNextTickAsync(token)` keeps the loop in user code.
- Cancel the page token, await the loop, then dispose the timer.

## Pattern

```csharp
private readonly CancellationTokenSource _cts = new();
private readonly CancellationToken _lifetimeToken;
private PeriodicTimer? _refreshTimer;
private Task? _refreshLoop;

public MyPage()
{
    _lifetimeToken = _cts.Token;
}

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (!firstRender)
    {
        return;
    }

    if (await LoadDataAsync() && _autoRefreshEnabled)
    {
        StartAutoRefresh();
    }
}

private void StartAutoRefresh()
{
    if (_lifetimeToken.IsCancellationRequested)
    {
        return;
    }

    StopAutoRefresh();
    _refreshTimer = new PeriodicTimer(TimeSpan.FromSeconds(30));
    _refreshLoop = RefreshLoopAsync(_refreshTimer, _lifetimeToken);
}

private void StopAutoRefresh()
{
    _refreshTimer?.Dispose();
    _refreshTimer = null;
}

private async Task RefreshLoopAsync(PeriodicTimer timer, CancellationToken token)
{
    try
    {
        while (await timer.WaitForNextTickAsync(token))
        {
            await InvokeAsync(RefreshDataAsync);
        }
    }
    catch (OperationCanceledException)
    {
    }
}

public async ValueTask DisposeAsync()
{
    await _cts.CancelAsync();

    if (_refreshLoop is not null)
    {
        try
        {
            await _refreshLoop;
        }
        catch (OperationCanceledException)
        {
        }
    }

    _refreshTimer?.Dispose();
    _cts.Dispose();
}
```

## Rules

- Snapshot the lifetime token once; do not read `_cts.Token` after disposal windows.
- Catch `OperationCanceledException` silently on navigation and shutdown.
- Use the same token for refresh work and page-scoped async calls.
- Keep the loop page-local; do not extract a shared base class just for two pages.
- Use `StopAutoRefresh()` before re-creating the timer on interval changes.
- Guard `StartAutoRefresh()` with `IsCancellationRequested` so late lifecycle continuations do nothing.
