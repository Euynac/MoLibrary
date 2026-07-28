using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.Execution;

namespace Monica.Core.Mediator;

/// <summary>
/// Default request dispatcher for Monica request handlers.
/// </summary>
public sealed class Mediator(IServiceProvider serviceProvider) : IMediator
{
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), Func<Mediator, object, CancellationToken, Task<object?>>> _dispatcherCache = new();
    private static readonly ConcurrentDictionary<(Type HandlerType, Type RequestType, Type ResponseType), ExecutionDescriptor> _descriptorCache = new();

    /// <inheritdoc />
    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dispatcher = _dispatcherCache.GetOrAdd(
            (request.GetType(), typeof(TResponse)),
            static key => CreateDispatcher(key.RequestType, key.ResponseType));

        var response = await dispatcher(this, request, cancellationToken).ConfigureAwait(false);
        return (TResponse)response!;
    }

    private static Func<Mediator, object, CancellationToken, Task<object?>> CreateDispatcher(
        Type requestType,
        Type responseType)
    {
        var mediatorParameter = Expression.Parameter(typeof(Mediator), "mediator");
        var requestParameter = Expression.Parameter(typeof(object), "request");
        var cancellationTokenParameter = Expression.Parameter(typeof(CancellationToken), "cancellationToken");

        var executeMethod = typeof(Mediator)
            .GetMethod(nameof(Execute), BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(requestType, responseType);

        var executeCall = Expression.Call(
            mediatorParameter,
            executeMethod,
            Expression.Convert(requestParameter, requestType),
            cancellationTokenParameter);

        var boxResultMethod = typeof(Mediator)
            .GetMethod(nameof(BoxResultAsync), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(responseType);

        var boxedCall = Expression.Call(boxResultMethod, executeCall);

        return Expression.Lambda<Func<Mediator, object, CancellationToken, Task<object?>>>(
            boxedCall,
            mediatorParameter,
            requestParameter,
            cancellationTokenParameter).Compile();
    }

    private async Task<TResponse> Execute<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken)
        where TRequest : IRequest<TResponse>
    {
        var handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        var descriptor = _descriptorCache.GetOrAdd(
            (handler.GetType(), typeof(TRequest), typeof(TResponse)),
            static key => CreateDescriptor(key.HandlerType, key.RequestType, key.ResponseType));
        var context = new ExecutionContext<TRequest>(
            descriptor,
            request,
            handler,
            cancellationToken);

        return await serviceProvider
            .GetRequiredService<IExecutionPipeline>()
            .ExecuteAsync(context, () => handler.Handle(request, cancellationToken))
            .ConfigureAwait(false);
    }

    private static ExecutionDescriptor CreateDescriptor(Type handlerType, Type requestType, Type responseType)
    {
        var contract = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
        var entryMethod = handlerType.GetInterfaceMap(contract).TargetMethods.Single();

        return new ExecutionDescriptor(
            MediatorExecutionPoints.Request,
            $"{handlerType.FullName}.{entryMethod.Name}",
            handlerType,
            entryMethod,
            requestType,
            responseType,
            isBusinessOperation: true,
            isLongRunning: false);
    }

    private static async Task<object?> BoxResultAsync<TResponse>(Task<TResponse> responseTask)
    {
        return await responseTask.ConfigureAwait(false);
    }
}
