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
        var descriptor = CreateDescriptor<ExecutionUnit>(invocation);
        await executionPipeline.ExecuteAsync(
            descriptor,
            CreateInput(invocation),
            invocation.TargetObject,
            invocation.ProceedAsync,
            FindCancellationToken(invocation.Method, invocation.Arguments));
    }

    private async Task ExecuteWithResultAsync<TResult>(IMethodInvocation invocation)
    {
        var descriptor = CreateDescriptor<TResult>(invocation);
        var result = await executionPipeline.ExecuteAsync(
            descriptor,
            CreateInput(invocation),
            invocation.TargetObject,
            async () =>
            {
                await invocation.ProceedAsync();
                return (TResult)invocation.ReturnValue;
            },
            FindCancellationToken(invocation.Method, invocation.Arguments));
        invocation.ReturnValue = result!;
    }

    private static ExecutionDescriptor CreateDescriptor<TResult>(IMethodInvocation invocation)
    {
        var method = invocation.Method;
        var componentType = method.DeclaringType ?? invocation.TargetObject.GetType();
        return ExecutionDescriptor.ForMethod<DynamicProxyMethodInput, TResult>(
            DynamicProxyExecutionPoints.Method,
            componentType,
            method,
            isBusinessOperation: true,
            transactionMode: ExecutionTransactionMode.Automatic);
    }

    private static DynamicProxyMethodInput CreateInput(IMethodInvocation invocation)
    {
        return new DynamicProxyMethodInput(
            invocation.Method,
            invocation.Arguments.ToArray(),
            invocation.GenericArguments.ToArray());
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
}
