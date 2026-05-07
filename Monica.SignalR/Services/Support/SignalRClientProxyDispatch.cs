using System.Reflection;
using System.Runtime.ExceptionServices;
using Monica.SignalR.Models;

namespace Monica.SignalR.Services.Support;

/// <summary>
/// Dispatch proxy that records observed strongly typed SignalR client invocations.
/// </summary>
internal sealed class SignalRClientProxyDispatch<TContract> : DispatchProxy
    where TContract : class
{
    private TContract? _inner;
    private SignalRSendDiagnosticsService? _diagnostics;
    private string? _hubName;
    private SignalRSendTarget? _target;
    private static readonly MethodInfo _trackGenericTaskMethod = typeof(SignalRClientProxyDispatch<TContract>)
        .GetMethod(nameof(TrackGenericTask), BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("The SignalR diagnostics generic task tracker could not be resolved.");

    /// <summary>
    /// Creates a tracked strongly typed SignalR client proxy.
    /// </summary>
    public static TContract Create(
        TContract inner,
        SignalRSendDiagnosticsService diagnostics,
        string hubName,
        SignalRSendTarget target)
    {
        var proxy = DispatchProxy.Create<TContract, SignalRClientProxyDispatch<TContract>>();
        var dispatch = (SignalRClientProxyDispatch<TContract>)(object)proxy;
        dispatch._inner = inner;
        dispatch._diagnostics = diagnostics;
        dispatch._hubName = hubName;
        dispatch._target = target;
        return proxy;
    }

    /// <inheritdoc />
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (_inner is null || _diagnostics is null || _hubName is null || _target is null)
        {
            throw new InvalidOperationException("The SignalR diagnostics proxy was not initialized.");
        }

        if (targetMethod is null)
        {
            throw new InvalidOperationException("The SignalR client method cannot be resolved.");
        }

        var operation = _diagnostics.StartObservedSend(_hubName, targetMethod.Name, _target);
        object? result;

        try
        {
            result = targetMethod.Invoke(_inner, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            operation.Complete(failed: true);
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
        catch
        {
            operation.Complete(failed: true);
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

        operation.Complete(failed: false);
        return result;
    }

    private static async Task TrackTask(Task task, SignalRSendDiagnosticsOperation operation)
    {
        try
        {
            await task.ConfigureAwait(false);
            operation.Complete(failed: false);
        }
        catch
        {
            operation.Complete(failed: true);
            throw;
        }
    }

    private static async Task<TResult> TrackGenericTask<TResult>(
        Task<TResult> task,
        SignalRSendDiagnosticsOperation operation)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            operation.Complete(failed: false);
            return result;
        }
        catch
        {
            operation.Complete(failed: true);
            throw;
        }
    }
}
