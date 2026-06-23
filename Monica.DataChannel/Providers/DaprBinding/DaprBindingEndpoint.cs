using System.Text.Json;
using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Monica.Core.Modularity.Extensions;
using Monica.Core.Modularity.Models;
using Monica.DataChannel.Abstractions;
using Monica.DataChannel.Abstractions.Communication;
using Monica.DataChannel.Abstractions.Partitioning;
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
                    endpoints.MapPost($"{metadata.InputListenerRoute}", async ([FromBody] JsonElement body, HttpResponse response, HttpContext context) =>
                    {
                        var dataContext = new ChannelDataContext(ChannelSide.Outer, body);
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
                    .WithMonicaEndpoint()
                    .WithName("DaprBinding路由")
                    .WithTags("基础功能")
                    .WithSummary("DaprBinding路由")
                    .WithDescription("DaprBinding路由");
                });
            }
        }
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
