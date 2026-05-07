using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Monica.SignalR.Metrics;

/// <summary>
/// Dispatch proxy that records observed strongly typed SignalR client invocations.
/// </summary>
internal sealed class SignalRSendMetricsDispatch<TContract> : DispatchProxy
    where TContract : class
{
    private TContract? _inner;
    private SignalRSendMetrics? _metrics;
    private string? _hubName;
    private SignalRSendTarget? _target;
    private static readonly MethodInfo _trackGenericTaskMethod = typeof(SignalRSendMetricsDispatch<TContract>)
        .GetMethod(nameof(TrackGenericTask), BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("The SignalR metrics generic task tracker could not be resolved.");

    /// <summary>
    /// Creates a tracked strongly typed SignalR client proxy.
    /// </summary>
    public static TContract Create(
        TContract inner,
        SignalRSendMetrics metrics,
        string hubName,
        SignalRSendTarget target)
    {
        var proxy = DispatchProxy.Create<TContract, SignalRSendMetricsDispatch<TContract>>();
        var dispatch = (SignalRSendMetricsDispatch<TContract>)(object)proxy;
        dispatch._inner = inner;
        dispatch._metrics = metrics;
        dispatch._hubName = hubName;
        dispatch._target = target;
        return proxy;
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (_inner is null || _metrics is null || _hubName is null || _target is null)
        {
            throw new InvalidOperationException("The SignalR metrics proxy was not initialized.");
        }

        if (targetMethod is null)
        {
            throw new InvalidOperationException("The SignalR client method cannot be resolved.");
        }

        var operation = _metrics.StartObservedSend(_hubName, targetMethod.Name, _target);
        object? result;

        try
        {
            result = targetMethod.Invoke(_inner, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            operation.Complete(ex.InnerException);
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
        catch (Exception ex)
        {
            operation.Complete(ex);
            throw;
        }

        if (result is Task task)
        {
            if (targetMethod.ReturnType == typeof(Task))
            {
                return TrackTask(task, operation);
            }

            if (targetMethod.ReturnType.IsGenericType &&
                targetMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                return _trackGenericTaskMethod
                    .MakeGenericMethod(targetMethod.ReturnType.GetGenericArguments()[0])
                    .Invoke(null, [task, operation]);
            }
        }

        operation.Complete();
        return result;
    }

    private static async Task TrackTask(Task task, SignalRSendMetricOperation operation)
    {
        try
        {
            await task.ConfigureAwait(false);
            operation.Complete();
        }
        catch (Exception ex)
        {
            operation.Complete(ex);
            throw;
        }
    }

    private static async Task<TResult> TrackGenericTask<TResult>(
        Task<TResult> task,
        SignalRSendMetricOperation operation)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            operation.Complete();
            return result;
        }
        catch (Exception ex)
        {
            operation.Complete(ex);
            throw;
        }
    }
}
