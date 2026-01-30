using System.Text.Json;
using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Monica.DataChannel.CoreCommunication;
using Monica.DataChannel.Interfaces;
using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.CoreCommunicationProvider.DaprBinding;

public class DaprBindingCore(MetadataForDaprBinding metadata, DaprClient client) : CommunicationCore<MetadataForDaprBinding>(metadata), IDynamicConfigApplicationBuilder
{
    private static readonly HashSet<string> _registeredRoutes = [];

    public override async Task ReceiveDataAsync(DataContext data)
    {
        if (metadata.Type == ECommunicationType.MQ)
        {
            if (string.IsNullOrWhiteSpace(metadata.OutputBindingName)) return;
            if (data.Data is null) return;
            await client.InvokeBindingAsync(metadata.OutputBindingName, "create", data.Data);
        }
   
    }

    public void ConfigApplicationBuilder(IApplicationBuilder app)
    {
        
    }

    public override EConnectionDirection SupportedConnectionDirection()
    {
        return EConnectionDirection.InputAndOutput;
    }

    public void ConfigEndpoints(IApplicationBuilder app)
    {
        if (metadata.Type == ECommunicationType.MQ)
        {
            if (string.IsNullOrWhiteSpace(metadata.InputListenerRoute)) return;
            if (_registeredRoutes.Add(metadata.InputListenerRoute))
            {
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapPost($"{metadata.InputListenerRoute}", async ([FromBody] JsonElement body, HttpResponse response, HttpContext context) =>
                    {
                        await SendDataAsync(new DataContext(EDataSource.Outer, body));
                    })
                    .WithName("DaprBinding路由")
                    .WithTags("基础功能")
                    .WithSummary("DaprBinding路由")
                    .WithDescription("DaprBinding路由");
                });
            }
        }
    }
}