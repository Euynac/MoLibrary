using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Monica.Core.ExceptionHandling.Exceptions;
using Monica.Core.Features.MoChainTracing;
using Monica.Core.Features.MoChainTracing.Models;
using Monica.Core.Features.MoTimekeeper;
using Monica.DependencyInjection.DynamicProxy;
using Monica.DependencyInjection.DynamicProxy.Abstract;
using Monica.DomainDrivenDesign.AutoController.MoRpc;
using Monica.Tool.Extensions;
using Monica.Tool.Results;

namespace Monica.Framework.Features.FrameworkChainTracing;

public record InvocationInfo(MethodInfo MethodInfo)
{
    public string HandlerName => MethodInfo.ReflectedType?.Name
                                   ?? MethodInfo.DeclaringType?.Name
                                   ?? "Unknown";

    public string OperationName => MethodInfo.Name;

    /// <summary>
    /// Whether it is a remote call, the call chain needs to be merged
    /// </summary>
    public bool IsRemoteCall => MethodInfo.DeclaringType?.IsImplementInterface<IMoRpcApi>() is true;

    public EChainTracingType GetInvocationType()
    {
        if (IsRemoteCall) return EChainTracingType.RemoteService;
        return EChainTracingType.Unknown;
    }
}


/// <summary>
/// Method-invocation chain-tracing interceptor built on the current ChainTracking pipeline.
/// Records chain data automatically for methods that return <see cref="IResultEnvelope" />.
/// </summary>
/// <param name="chainTracing">Call chain tracking service</param>
/// <param name="timekeeperFactory">timer factory</param>
/// https://kozmic.net/dynamic-proxy-tutorial/
/// https://github.com/moframework/mo/issues/14378
/// https://docs.mo.io/en/mo/7.4/Dependency-Injection#advanced-features
public class ChainTrackingProviderInvocationInterceptor(
    IMoChainTracing chainTracing,
    IMoTimekeeperFactory timekeeperFactory) : MoInterceptor
{
    /// <summary>
    /// Determine whether the call chain should be recorded
    /// </summary>
    /// <param name="invocation">Method call information</param>
    /// <param name="info"></param>
    /// <returns>Whether the call chain should be logged</returns>
    private static bool ShouldRecordChain(IMoMethodInvocation invocation, [NotNullWhen(true)] out InvocationInfo? info)
    {
        var returnType = invocation.Method.ReturnType;
        info = null;
        // If it is Task<T>, get the type of T
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            returnType = returnType.GetGenericArguments()[0];
        }
        
        // Record chain data only for result-envelope return types.
        var shouldRecord = returnType.IsImplementInterface(typeof(IResultEnvelope));
        
        if (shouldRecord)
        {
            info = new InvocationInfo(invocation.Method);
            return true;
        }
        
        return false;
    }



    /// <summary>
    /// Intercepting method calls
    /// </summary>
    /// <param name="invocation">Method call information</param>
    public override async Task InterceptAsync(IMoMethodInvocation invocation)
    {
        if (!ShouldRecordChain(invocation, out var info))
        {
            // There is no need to record the method of the call chain, execute it directly
            try
            {
                await invocation.ProceedAsync();
            }
            catch (Exception ex)
            {
                // For methods excluded from chain tracing, wrap the exception and attach invocation context.
                throw new ContextualException(
                    $"执行方法 {invocation.Method.DeclaringType?.Name}.{invocation.Method.Name} 异常",
                    ex)
                    .WithMetadata("declaringType", invocation.Method.DeclaringType?.Name)
                    .WithMetadata("methodName", invocation.Method.Name)
                    .WithMetadata("invocationContext", "ChainTrackingProviderInvocationInterceptor");
            }
            return;
        }

        var isRemoteCall = info.IsRemoteCall;

        // Start call chain tracing
        using var scope =
            chainTracing.BeginScope(info.OperationName, info.HandlerName, type: info.GetInvocationType());

        // Create timer
        using var timer = timekeeperFactory.CreateNormalTimer(info.HandlerName);
        timer.Start();
        
        try
        {
            await invocation.ProceedAsync();
            timer.Finish();

            // Handle successful response
            var responseTypeName = ChainTracingHelper.GetResponseTypeName(invocation.Method.ReturnType);
            
            if (invocation.ReturnValue is IResultEnvelope response)
            {
                var success = response.Status == ResStatus.Ok;
                var resultDescription =
                    $"{responseTypeName}({response.Status}){(response.Message?.LimitMaxLength(1000, "...").BeNullIfWhiteSpace() is {} msg ? $"[{msg}]" : null)}";

                if (isRemoteCall)
                {
                    scope.MergeRemoteChain(response);
                }

                if (success)
                {
                    scope.EndWithSuccess(resultDescription);
                }
                else
                {
                    scope.EndWithFailure(resultDescription);
                }
            }
        }
        catch (Exception ex)
        {
            timer.Finish();

            // Log exceptions to the call chain
            scope.EndWithException(ex, $"执行方法 {invocation.Method.DeclaringType?.Name}.{invocation.Method.Name} 异常");

            throw;
        }
    }
}
