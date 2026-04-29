using Monica.AI.KnowledgeBase.Facades;
using Monica.AI.KnowledgeBase.Models;
using Monica.Core.Results;

namespace Monica.AI.UI.UIRAG.State;

/// <summary>
/// Owns document-queue polling and throttled progress refresh for the RAG manage page.
/// </summary>
public sealed class RAGQueuePollingState(KnowledgeBaseFacade knowledgeBaseFacade) : IDisposable
{
    private const int QueueRefreshIntervalMs = 700;
    private static readonly TimeSpan ProgressQueueRefreshInterval = TimeSpan.FromSeconds(1);

    private CancellationTokenSource? _pollingCancellationTokenSource;
    private Task? _pollingTask;
    private string? _activeKnowledgeBaseId;
    private DateTimeOffset _lastProgressRefreshAtUtc = DateTimeOffset.MinValue;
    private int _progressRefreshInFlight;

    /// <summary>
    /// Raised when the queue was refreshed successfully.
    /// </summary>
    public event Action<IReadOnlyList<DocumentQueueItem>>? QueueRefreshed;

    /// <summary>
    /// Raised when queue refresh failed.
    /// </summary>
    public event Action<string>? QueueRefreshFailed;

    private bool IsPolling => _pollingTask is { IsCompleted: false };

    /// <summary>
    /// Start polling for one knowledge base while active queue work exists.
    /// </summary>
    public void EnsurePolling(string knowledgeBaseId, Func<bool> hasActiveWork)
    {
        if (string.IsNullOrWhiteSpace(knowledgeBaseId))
        {
            Stop();
            return;
        }

        if (!hasActiveWork())
        {
            Stop();
            return;
        }

        if (!string.Equals(_activeKnowledgeBaseId, knowledgeBaseId, StringComparison.OrdinalIgnoreCase))
        {
            Stop();
        }

        if (IsPolling)
        {
            return;
        }

        _activeKnowledgeBaseId = knowledgeBaseId;
        _pollingCancellationTokenSource = new CancellationTokenSource();
        _pollingTask = RunPollingLoopAsync(knowledgeBaseId, hasActiveWork, _pollingCancellationTokenSource.Token);
    }

    /// <summary>
    /// Trigger one throttled refresh from progress callbacks without starting a second polling loop.
    /// </summary>
    public async Task RefreshFromProgressAsync(string knowledgeBaseId)
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastProgressRefreshAtUtc < ProgressQueueRefreshInterval)
        {
            return;
        }

        if (Interlocked.Exchange(ref _progressRefreshInFlight, 1) == 1)
        {
            return;
        }

        try
        {
            _lastProgressRefreshAtUtc = now;
            await RefreshQueueAsync(knowledgeBaseId);
        }
        finally
        {
            Interlocked.Exchange(ref _progressRefreshInFlight, 0);
        }
    }

    /// <summary>
    /// Stop the current polling loop.
    /// </summary>
    public void Stop()
    {
        var cancellationTokenSource = _pollingCancellationTokenSource;
        if (cancellationTokenSource is null)
        {
            _activeKnowledgeBaseId = null;
            _pollingTask = null;
            return;
        }

        _pollingCancellationTokenSource = null;
        _pollingTask = null;
        _activeKnowledgeBaseId = null;

        if (!cancellationTokenSource.IsCancellationRequested)
        {
            cancellationTokenSource.Cancel();
        }

        cancellationTokenSource.Dispose();
    }

    private async Task RunPollingLoopAsync(
        string knowledgeBaseId,
        Func<bool> hasActiveWork,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await RefreshQueueAsync(knowledgeBaseId);

                if (!hasActiveWork())
                {
                    break;
                }

                await Task.Delay(QueueRefreshIntervalMs, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation when the page is disposed or polling target switches.
        }
        finally
        {
            if (_pollingCancellationTokenSource?.Token == cancellationToken)
            {
                _pollingCancellationTokenSource.Dispose();
                _pollingCancellationTokenSource = null;
                _pollingTask = null;
                _activeKnowledgeBaseId = null;
            }
        }
    }

    private async Task RefreshQueueAsync(string knowledgeBaseId)
    {
        var result = await knowledgeBaseFacade.GetDocumentInventoryAsync(knowledgeBaseId);
        if (result.IsFailed(out var error, out var queue))
        {
            QueueRefreshFailed?.Invoke(error.Message ?? "Failed to load document queue.");
            return;
        }

        QueueRefreshed?.Invoke(queue);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Stop();
    }
}
