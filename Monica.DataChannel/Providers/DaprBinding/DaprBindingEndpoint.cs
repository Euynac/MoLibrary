using System.Text.Json;
using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Monica.Core.Modularity.Extensions;
using Monica.DataChannel.Abstractions;
using Monica.DataChannel.Abstractions.Communication;
using Monica.DataChannel.Abstractions.Partitioning;
using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.Providers.DaprBinding;

/// <summary>
/// Connects a data-channel pipeline to Dapr input and output bindings.
/// </summary>
/// <param name="metadata">The binding direction, route, component name, and partition settings.</param>
/// <param name="client">The current host's Dapr client.</param>
/// <param name="partitionKeyResolver">An optional resolver for output-binding partition metadata.</param>
/// <param name="inputDispatcher">An optional host service that dispatches input messages before pipeline processing.</param>
public class DaprBindingEndpoint(
    DaprBindingOptions metadata,
    DaprClient client,
    IDataChannelPartitionKeyResolver? partitionKeyResolver = null,
    IDaprBindingInputDispatcher? inputDispatcher = null)
    : CommunicationEndpointBase<DaprBindingOptions>(metadata), IApplicationBuilderConfigurable
{
    private const string ROUTE_REGISTRY_KEY = "Monica.DataChannel.DaprBinding.RouteRegistry";

    /// <inheritdoc />
    public override async Task ReceiveDataAsync(ChannelDataContext data)
    {
        if (metadata.Type != CommunicationType.MQ ||
            string.IsNullOrWhiteSpace(metadata.OutputBindingName) ||
            data.Data is null)
        {
            return;
        }

        await client.InvokeBindingAsync(
            metadata.OutputBindingName,
            "create",
            data.Data,
            await ResolveOutputMetadataAsync(data));
    }

    /// <inheritdoc />
    public void ConfigApplicationBuilder(IApplicationBuilder app)
    {
    }

    /// <inheritdoc />
    public override ConnectionDirection SupportedConnectionDirection()
    {
        return ConnectionDirection.InputAndOutput;
    }

    /// <inheritdoc />
    public void ConfigEndpoints(IApplicationBuilder app)
    {
        var route = metadata.InputListenerRoute;
        if (metadata.Type != CommunicationType.MQ || string.IsNullOrWhiteSpace(route))
        {
            return;
        }

        if (!ClaimRoute(app, route))
        {
            return;
        }

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapPost(route, async ([FromBody] JsonElement body, HttpContext context) =>
            {
                // Dapr can invoke multiple partitions, replicas, or sidecars concurrently. Each callback
                // awaits the pipeline so the HTTP response still represents completion of that message.
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
            .WithName($"DataChannel.DaprBinding:{route}")
            .WithTags("DataChannel")
            .WithSummary("Receive a Dapr input binding message")
            .WithDescription("Forwards a Dapr input binding payload into its configured data-channel pipeline.");
        });
    }

    private bool ClaimRoute(IApplicationBuilder app, string route)
    {
        lock (app.Properties)
        {
            if (!app.Properties.TryGetValue(ROUTE_REGISTRY_KEY, out var value) ||
                value is not Dictionary<string, DaprBindingEndpoint> routes)
            {
                routes = new Dictionary<string, DaprBindingEndpoint>(StringComparer.OrdinalIgnoreCase);
                app.Properties[ROUTE_REGISTRY_KEY] = routes;
            }

            if (!routes.TryGetValue(route, out var owner))
            {
                routes.Add(route, this);
                return true;
            }

            if (ReferenceEquals(owner, this))
            {
                return false;
            }

            throw new InvalidOperationException(
                $"Dapr input binding route '{route}' is already owned by another data channel in the current host.");
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
