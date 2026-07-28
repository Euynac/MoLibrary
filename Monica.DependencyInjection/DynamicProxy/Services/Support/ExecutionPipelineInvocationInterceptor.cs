using System.Collections.Concurrent;
using System.Reflection;
using Monica.Core.Execution;
using Monica.DependencyInjection.DynamicProxy.Abstractions;
using Monica.DependencyInjection.DynamicProxy.Models;

namespace Monica.DependencyInjection.DynamicProxy.Services.Support;

/// <summary>
/// Adapts Task-returning Castle proxy invocations to Monica's typed execution pipeline.
/// </summary>
internal sealed class ExecutionPipelineInvocationInterceptor(IExecutionPipeline executionPipeline)
    : InvocationInterceptor
{
    private delegate Task ResultInvocation(
        ExecutionPipelineInvocationInterceptor interceptor,
        IMethodInvocation invocation);

    private static readonly MethodInfo EXECUTE_WITH_RESULT_METHOD =
        typeof(ExecutionPipelineInvocationInterceptor).GetMethod(
            nameof(ExecuteWithResultAsync),
            BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static readonly ConcurrentDictionary<Type, ResultInvocation> RESULT_INVOCATIONS = new();
    private static readonly ConcurrentDictionary<DescriptorCacheKey, ExecutionDescriptor> DESCRIPTORS = new();

    /// <inheritdoc />
    public override Task InterceptAsync(IMethodInvocation invocation)
    {
        var returnType = invocation.Method.ReturnType;
        if (returnType == typeof(Task))
        {
            return ExecuteWithoutResultAsync(invocation);
        }

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var resultType = returnType.GetGenericArguments()[0];
            var resultInvocation = RESULT_INVOCATIONS.GetOrAdd(resultType, CreateResultInvocation);
            return resultInvocation(this, invocation);
        }

        return invocation.ProceedAsync();
    }

    private async Task ExecuteWithoutResultAsync(IMethodInvocation invocation)
    {
        var context = CreateContext(invocation, typeof(ExecutionUnit));
        await executionPipeline.ExecuteAsync<DynamicProxyMethodInput, ExecutionUnit>(
            context,
            async () =>
            {
                await invocation.ProceedAsync();
                return ExecutionUnit.Value;
            });
    }

    private async Task ExecuteWithResultAsync<TResult>(IMethodInvocation invocation)
    {
        var context = CreateContext(invocation, typeof(TResult));
        var result = await executionPipeline.ExecuteAsync<DynamicProxyMethodInput, TResult>(
            context,
            async () =>
            {
                await invocation.ProceedAsync();
                return (TResult)invocation.ReturnValue;
            });
        invocation.ReturnValue = result!;
    }

    private static ExecutionContext<DynamicProxyMethodInput> CreateContext(
        IMethodInvocation invocation,
        Type resultType)
    {
        var method = invocation.Method;
        var componentType = method.DeclaringType ?? invocation.TargetObject.GetType();
        var descriptor = DESCRIPTORS.GetOrAdd(
            new DescriptorCacheKey(method, componentType, resultType),
            static key => new ExecutionDescriptor(
                DynamicProxyExecutionPoints.Method,
                $"{key.ComponentType.FullName ?? key.ComponentType.Name}.{key.Method.Name}",
                key.ComponentType,
                key.Method,
                typeof(DynamicProxyMethodInput),
                key.ResultType,
                isBusinessOperation: true,
                isLongRunning: false));
        var input = new DynamicProxyMethodInput(
            method,
            invocation.Arguments.ToArray(),
            invocation.GenericArguments.ToArray());

        return new ExecutionContext<DynamicProxyMethodInput>(
            descriptor,
            input,
            invocation.TargetObject,
            FindCancellationToken(method, invocation.Arguments));
    }

    private static CancellationToken FindCancellationToken(MethodInfo method, IReadOnlyList<object> arguments)
    {
        var parameters = method.GetParameters();
        for (var index = 0; index < parameters.Length; index++)
        {
            if (parameters[index].ParameterType == typeof(CancellationToken)
                && arguments[index] is CancellationToken cancellationToken)
            {
                return cancellationToken;
            }
        }

        return default;
    }

    private static ResultInvocation CreateResultInvocation(Type resultType)
    {
        return EXECUTE_WITH_RESULT_METHOD
            .MakeGenericMethod(resultType)
            .CreateDelegate<ResultInvocation>();
    }

    private readonly record struct DescriptorCacheKey(
        MethodInfo Method,
        Type ComponentType,
        Type ResultType);
}
