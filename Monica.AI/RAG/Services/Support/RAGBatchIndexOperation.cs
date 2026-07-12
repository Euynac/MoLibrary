using Monica.AI.RAG.Models;

namespace Monica.AI.RAG.Services.Support;

/// <summary>
/// Owns cancellation, progress, and terminal state for one knowledge-base batch indexing run.
/// </summary>
internal sealed class RAGBatchIndexOperation(string knowledgeBaseId) : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly CancellationTokenSource _cancellation = new();
    private RAGBatchIndexOperationState _state = RAGBatchIndexOperationState.Running;

    /// <summary>Knowledge base being indexed.</summary>
    public string KnowledgeBaseId { get; } = knowledgeBaseId;

    /// <summary>Cancellation token owned by this operation.</summary>
    public CancellationToken CancellationToken => _cancellation.Token;

    /// <summary>Most recently reported indexing progress.</summary>
    public IndexingProgress? Progress { get; private set; }

    /// <summary>Current operation state.</summary>
    public RAGBatchIndexOperationState State
    {
        get
        {
            lock (_syncRoot)
            {
                return _state;
            }
        }
    }

    /// <summary>Requests cancellation when the operation is still active.</summary>
    public string RequestCancellation()
    {
        lock (_syncRoot)
        {
            if (_state == RAGBatchIndexOperationState.CancellationRequested)
            {
                return "Cancellation already requested.";
            }

            if (_state != RAGBatchIndexOperationState.Running)
            {
                return "The indexing operation has already completed.";
            }

            _state = RAGBatchIndexOperationState.CancellationRequested;
        }

        _cancellation.Cancel();
        return "Cancellation requested.";
    }

    /// <summary>Records the latest aggregate progress snapshot.</summary>
    public void ReportProgress(IndexingProgress progress)
    {
        lock (_syncRoot)
        {
            Progress = progress;
        }
    }

    /// <summary>Moves the operation to its terminal state.</summary>
    public void Complete(RAGBatchIndexOperationState terminalState)
    {
        if (terminalState is RAGBatchIndexOperationState.Running
            or RAGBatchIndexOperationState.CancellationRequested)
        {
            throw new ArgumentOutOfRangeException(nameof(terminalState), terminalState, "A terminal state is required.");
        }

        lock (_syncRoot)
        {
            _state = terminalState;
        }
    }

    /// <inheritdoc />
    public void Dispose() => _cancellation.Dispose();
}

/// <summary>Lifecycle states for a batch indexing operation.</summary>
internal enum RAGBatchIndexOperationState
{
    Running,
    CancellationRequested,
    Succeeded,
    Failed,
    Cancelled
}
