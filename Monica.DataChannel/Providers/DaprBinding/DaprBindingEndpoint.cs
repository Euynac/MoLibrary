using System.Text.Json;
using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Monica.Core.Modularity.Extensions;
using Monica.DataChannel.Abstractions;
using Monica.DataChannel.Abstractions.Communication;
using Monica.DataChannel.Abstractions.Partitioning;
using Monica.DataChannel.Middlewares;
using Monica.DataChannel.Models.DaprBinding;
using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.Providers.DaprBinding;

public class DaprBindingEndpoint(
    DaprBindingOptions metadata,
    DaprClient client,
    IDataChannelPartitionKeyResolver? partitionKeyResolver = null,
    IDaprBindingInputDispatcher? inputDispatcher = null)
    : CommunicationEndpointBase<DaprBindingOptions>(metadata), IApplicationBuilderConfigurable
{
    private static readonly HashSet<string> _registeredRoutes = [];
    private readonly MessageReceiveDiagnostics _messageReceiveDiagnostics = new(metadata.InputListenerRoute);

    public override async Task ReceiveDataAsync(ChannelDataContext data)
    {
        if (metadata.Type == CommunicationType.MQ)
        {
            if (string.IsNullOrWhiteSpace(metadata.OutputBindingName)) return;
            if (data.Data is null) return;
            await client.InvokeBindingAsync(metadata.OutputBindingName, "create", data.Data, await ResolveOutputMetadataAsync(data));
        }
   
    }

    public void ConfigApplicationBuilder(IApplicationBuilder app)
    {
        
    }

    public override ConnectionDirection SupportedConnectionDirection()
    {
        return ConnectionDirection.InputAndOutput;
    }

    public void ConfigEndpoints(IApplicationBuilder app)
    {
        if (metadata.Type == CommunicationType.MQ)
        {
            if (string.IsNullOrWhiteSpace(metadata.InputListenerRoute)) return;
            if (_registeredRoutes.Add(metadata.InputListenerRoute))
            {
                app.UseEndpoints(endpoints =>
                {
                    //接收不是由我们项目代码串行化的；整体是“可并发接收”。更准确地说：单条 Dapr Binding 回调内部是同步 await 到处理完成；
                    //同一个 Kafka partition 内通常按顺序一条条处理；但多个 partition、多个副本/sidecar、或 Dapr 并发回调时，这里接收会并发进入。
                    endpoints.MapPost($"{metadata.InputListenerRoute}", async ([FromBody] JsonElement body, HttpResponse response, HttpContext context) =>
                    {
                        var dataContext = new ChannelDataContext(ChannelSide.Outer, body);
                        DecorateInputMetadata(dataContext, MessageReceiveDiagnosticsMiddleware.GetSnapshot(context));

                        if (metadata.EnableInputDispatcher && inputDispatcher != null)
                        {
                            var dispatched = await inputDispatcher.TryDispatchAsync(
                                dataContext,
                                async (message, _) => await SendDataAsync(message),
                                context.RequestAborted);

                            if (dispatched)
                            {
                                return;
                            }
                        }

                        await SendDataAsync(dataContext);
                    })
                    .AddEndpointFilter(new MessageReceiveDiagnosticsMiddleware(_messageReceiveDiagnostics))
                    .WithMonicaEndpoint()
                    .WithName("DaprBinding路由")
                    .WithTags("基础功能")
                    .WithSummary("DaprBinding路由")
                    .WithDescription("DaprBinding路由");
                });
            }
        }
    }

    private void DecorateInputMetadata(ChannelDataContext dataContext, MessageReceiveDiagnosticsSnapshot? receiveSnapshot)
    {
        if (receiveSnapshot is null) return;

        var metadata = (IDictionary<string, object?>)dataContext.Metadata;
        metadata["Message.Receive.Route"] = receiveSnapshot.Route;
        metadata["Message.Receive.TraceIdentifier"] = receiveSnapshot.TraceIdentifier;
        metadata["Message.Receive.IsConcurrentReceive"] = receiveSnapshot.IsConcurrentReceive;
        metadata["Message.Receive.ActiveReceiveCount"] = receiveSnapshot.ActiveReceiveCount;
        metadata["Message.Receive.MaxConcurrentReceiveCount"] = receiveSnapshot.MaxConcurrentReceiveCount;
        metadata["Message.Receive.MessagePerSecond"] = receiveSnapshot.MessagePerSecond;
        metadata["Message.Receive.LastSecondCompletedMessageCount"] = receiveSnapshot.LastSecondCompletedMessageCount;
        metadata["Message.Receive.TotalReceivedMessageCount"] = receiveSnapshot.TotalReceivedMessageCount;
        metadata["Message.Receive.ThreadId"] = receiveSnapshot.ThreadId;
        metadata["Message.Receive.TimestampUtc"] = receiveSnapshot.TimestampUtc;
    }

    private async Task<IReadOnlyDictionary<string, string>?> ResolveOutputMetadataAsync(ChannelDataContext data)
    {
        if (!metadata.EnableOutputPartitionMetadata || partitionKeyResolver == null)
        {
            return null;
        }

        var partitionKey = await partitionKeyResolver.ResolvePartitionKeyAsync(data);
        if (string.IsNullOrWhiteSpace(partitionKey))
        {
            return null; 
        }

        return new Dictionary<string, string>
        {
            [DataChannelPartitionConstants.MonicaPartitionKey] = partitionKey,
            [DataChannelPartitionConstants.PartitionKey] = partitionKey,
            [DataChannelPartitionConstants.MessageKey] = partitionKey
        };
    }
}
