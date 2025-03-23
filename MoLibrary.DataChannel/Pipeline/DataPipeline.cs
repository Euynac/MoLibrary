using Microsoft.Extensions.Logging;
using MoLibrary.DataChannel.CoreCommunication;
using MoLibrary.DataChannel.Interfaces;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.DataChannel.Pipeline;

public class DataPipeline
{
    /// <summary>
    /// Inner to Outer
    /// </summary>
    public IPipeEndpoint InnerEndpoint { get; set; }
    /// <summary>
    /// Outer to Inner
    /// </summary>
    public IPipeEndpoint OuterEndpoint { get; set; }

    /// <summary>
    /// 管道ID
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// 管道组ID
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// 是否已初始化
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// 是否已不可用（如初始化等存在致命错误）
    /// </summary>
    public bool IsNotAvailable { get; set; }

    internal DataPipeline(IPipeEndpoint innerEndpoint, IPipeEndpoint outerEndpoint, string id, string? groupId = null)
    {
        InnerEndpoint = innerEndpoint;
        OuterEndpoint = outerEndpoint;
        Id = id;
        GroupId = groupId;
    }

    public static DataPipelineBuilder Create() => new();

    public List<IPipeEndpointMiddleware> EndpointMiddlewares { get; private set; } = [];
    public List<IPipeTransformMiddleware> TransformMiddlewares { get; private set; } = [];

    internal IEnumerable<IPipeMiddleware> GetMiddlewares()
    {
        foreach (var endpointMiddleware in EndpointMiddlewares)
        {
            yield return endpointMiddleware;
        }

        foreach (var transformMiddleware in TransformMiddlewares)
        {
            yield return transformMiddleware;
        }
    }

    internal IEnumerable<IPipeEndpoint> GetEndpoints()
    {
        yield return InnerEndpoint;
        yield return OuterEndpoint;
    }

    internal IEnumerable<IPipeComponent> GetComponents()
    {
        foreach (var endpoint in GetEndpoints())
        {
            yield return endpoint;
        }

        foreach (var middleware in GetMiddlewares())
        {
            yield return middleware;
        }
    }


    internal void SetMiddlewares(IReadOnlyList<IPipeMiddleware> middlewares)
    {
        EndpointMiddlewares = middlewares.OfType<IPipeEndpointMiddleware>().ToList();
        TransformMiddlewares = middlewares.OfType<IPipeTransformMiddleware>().ToList();
    }

    public async Task SendDataAsync(DataContext data)
    {
        await TransformMiddlewares.DoAsync(async p => data = await p.PassAsync(data));
        if (data.Entrance == EDataSource.Outer)
        {
            await InnerEndpoint.ReceiveDataAsync(data);
        }
        else if(data.Entrance == EDataSource.Inner)
        {
            await OuterEndpoint.ReceiveDataAsync(data);
        }
        
    }


    internal async Task<Res> InitAsync()
    {
        InnerEndpoint.Pipe = this;
        OuterEndpoint.Pipe = this;
        GetMiddlewares().OfType<IWantAccessPipeline>().Do(p => p.Pipe = this);
        
        foreach (var communicationCore in GetEndpoints().OfType<ICommunicationCore>())
        {
            try
            {
                await communicationCore.InitAsync();
            }
            catch (Exception e)
            {
                IsNotAvailable = true;
                DataChannelCentral.Logger.LogError(e, "DataPipeline:{Id}初始化失败", Id);
                return (e, $"DataPipeline:{Id}初始化失败");
            }
        }

        IsInitialized = true;
        return Res.Ok();
    }

    internal async Task DisposeAsync()
    {
        await GetEndpoints().OfType<ICommunicationCore>().DoAsync(async p => await p.DisposeAsync());
        IsInitialized = false;
    }
}