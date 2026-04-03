using System.Runtime.CompilerServices;

namespace Monica.Tool.Threading;

/// <summary>
/// An alternative to ConfigureAwait(false) everywhere.
/// Removes the current synchronization context when awaited, allowing subsequent async operations
/// to run on thread pool threads instead of being marshalled back to the original context.
/// </summary>
/// <remarks>
/// Reference from: https://blogs.msdn.microsoft.com/benwilli/2017/02/09/an-alternative-to-configureawaitfalse-everywhere/
/// </remarks>
public struct SynchronizationContextRemover : INotifyCompletion
{
    public bool IsCompleted => SynchronizationContext.Current == null;

    public void OnCompleted(Action continuation)
    {
        var prevContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            continuation();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(prevContext);
        }
    }

    public SynchronizationContextRemover GetAwaiter()
    {
        return this;
    }

    public void GetResult()
    {
    }
}
