using System.Text.Json;
using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Monica.Core.Modularity.Models;
using Monica.DataChannel.Abstractions;
using Monica.DataChannel.Abstractions.Communication;
using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.Providers.DaprBinding;

public class DaprBindingEndpoint(DaprBindingOptions metadata, DaprClient client) : CommunicationEndpointBase<DaprBindingOptions>(metadata), IApplicationBuilderConfigurable
{
    private static readonly HashSet<string> _registeredRoutes = [];

    public override async Task ReceiveDataAsync(ChannelDataContext data)
    {
        if (metadata.Type == CommunicationType.MQ)
        {
            if (string.IsNullOrWhiteSpace(metadata.OutputBindingName)) return;
            if (data.Data is null) return;
            await client.InvokeBindingAsync(metadata.OutputBindingName, "create", data.Data);
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
                        await SendDataAsync(new ChannelDataContext(ChannelSide.Outer, body));
                    })
                    .WithMetadata(MonicaMinimalApiMetadata.Instance)
                    .WithName("DaprBinding路由")
                    .WithTags("基础功能")
                    .WithSummary("DaprBinding路由")
                    .WithDescription("DaprBinding路由");
                });
            }
        }
    }
}
