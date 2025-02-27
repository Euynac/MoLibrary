using BuildingBlocksPlatform.Features.GRPCExtensions;
using Dapr.Actors.Client;
using Dapr.Actors;

namespace BuildingBlocksPlatform.SeedWork;

public abstract class OurActorApi(IMoServiceProvider provider) : OurRpcApi(provider)
{
    public abstract string GetActorName();

    /// <summary>
    /// Invokes the specified method for the actor with argument. The argument will be serialized as JSON.
    /// </summary>
    /// <typeparam name="TRequest">The data type of the object that will be serialized.</typeparam>
    /// <typeparam name="TResponse">Return type of method.</typeparam>
    /// <param name="actorId"></param>
    /// <param name="method">Actor method name.</param>
    /// <param name="data">Object argument for actor method.</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    /// <returns>Response form server.</returns>
    public async Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(string actorId,
        string method,
        TRequest data,
        CancellationToken cancellationToken = default)
    {
        var actor = new ActorId(actorId);
        var dynamicProxy = ActorProxy.Create(actor, GetActorName());
        if (data is IHasGrpcHttpInfo httpInfo)
        {
            if (_accessor.HttpContext is {Request.Headers.Count: > 0} context)
            {
                foreach (var header in context.Request.Headers)
                {
                    httpInfo.AddHeader(header.Key, header.Value);
                }
            }
        }

        var test = await dynamicProxy.InvokeMethodAsync<TRequest, TResponse>(method,
            data, cancellationToken);
        return test;
    }

}
