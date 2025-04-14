using System.Text.Json;
using Dapr.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using MoLibrary.DataChannel.CoreCommunication;
using MoLibrary.DataChannel.Interfaces;
using MoLibrary.DataChannel.Pipeline;

namespace MoLibrary.DataChannel.CoreCommunicationProvider.DaprBinding;

public class DaprBindingCore(MetadataForDaprBinding metadata, DaprClient client) : CommunicationCore<MetadataForDaprBinding>(metadata), IDynamicConfigApplicationBuilder
{
    public override async Task ReceiveDataAsync(DataContext data)
    {
        if (metadata.Type == ECommunicationType.MQ)
        {
            if (string.IsNullOrWhiteSpace(metadata.OutputBindingName)) return;
            if (data.Data is null) return;
            await client.InvokeBindingAsync(metadata.OutputBindingName, "create", data.Data);
        }
   
    }

    public void DoConfigApplication(IApplicationBuilder app)
    {
        if (metadata.Type == ECommunicationType.MQ)
        {
            if (string.IsNullOrWhiteSpace(metadata.InputListenerRoute)) return;
            app.UseEndpoints(endpoints =>
            {
                var tagGroup = new List<OpenApiTag> { new() { Name = "基础功能", Description = "DaprBinding路由" } };
                endpoints.MapPost($"{metadata.InputListenerRoute}", async ([FromBody] JsonElement body, HttpResponse response, HttpContext context) =>
                {
                    await SendDataAsync(new DataContext(EDataSource.Outer, body));
                }).WithName("DaprBinding路由").WithOpenApi(operation =>
                {
                    operation.Summary = "DaprBinding路由";
                    operation.Description = "DaprBinding路由";
                    operation.Tags = tagGroup;
                    return operation;
                });
            });
        }
    }

    public override EConnectionDirection SupportedConnectionDirection()
    {
        return EConnectionDirection.InputAndOutput;
    }
}