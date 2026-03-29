using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Monica.Core.ExceptionHandling.Abstractions;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.DomainDrivenDesign.AutoController.MoRpc;
using Monica.Framework.ChainTracing.Abstractions;
using Monica.Framework.ChainTracing.Extensions;
using Monica.Framework.ChainTracing.Models;
using Monica.Modules;

namespace Monica.Framework.ChainTracing.Providers.MoRpc;

/// <summary>
/// Captures MoRpc actor responses and attaches chain-tracing metadata.
/// </summary>
internal sealed class MoRpcChainTracingMiddleware(
    IJsonSerializerOptionsProvider jsonSerializerOptionsProvider,
    IOptions<ModuleResultEnvelopeOption> resultEnvelopeOptions,
    IExceptionHandlerService handler,
    IChainTracing tracing) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.GetEndpoint()?.DisplayName != "Dapr Actors Invoke")
        {
            await next(context);
            return;
        }

        context.Request.EnableBuffering();

        if (context.Request.Body.CanRead)
        {
            try
            {
                context.Request.Body.Position = 0;

                if (JsonSerializer.Deserialize<MoRpcRequest>(context.Request.Body, jsonSerializerOptionsProvider.SerializerOptions) is { Headers.Count: > 0 } request)
                {
                    foreach (var (key, value) in request.Headers.Where(p => p.Key.StartsWith("X-")))
                    {
                        if (!context.Request.Headers.ContainsKey(key))
                        {
                            context.Request.Headers.Append(key, value);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                context.Request.Body.Position = 0;
            }
        }

        var originalBodyStream = context.Response.Body;

        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        try
        {
            using var scope = tracing.BeginScope(context.Request.Path.Value ?? "Actor", "Actor");
            await next(context);

            memoryStream.Position = 0;
            var jsonNode = await JsonSerializer.DeserializeAsync<JsonNode>(memoryStream, jsonSerializerOptionsProvider.SerializerOptions);

            scope.EndWithSuccess();

            if (tracing.GetCurrentChain() is { } chain && jsonNode is JsonObject jsonObject)
            {
                chain.MarkComplete();

                var metadataKey = resultEnvelopeOptions.Value.FieldNames.GetMetadataPropertyName(jsonSerializerOptionsProvider.SerializerOptions);
                var chainKey = jsonSerializerOptionsProvider.UsingJsonDictionaryKeyPolicy(ChainTraceContext.CHAIN_KEY);

                if (!jsonObject.ContainsKey(metadataKey) || jsonObject[metadataKey] is not JsonObject)
                {
                    jsonObject[metadataKey] = new JsonObject();
                }

                var metadataObject = jsonObject[metadataKey]!.AsObject();
                metadataObject[chainKey] = JsonSerializer.SerializeToNode(chain.Root, jsonSerializerOptionsProvider.SerializerOptions);
            }

            using var responseStream = new MemoryStream();
            context.Response.Body = responseStream;

            await context.Response.WriteAsJsonAsync(jsonNode, jsonSerializerOptionsProvider.SerializerOptions);

            responseStream.Position = 0;
            await responseStream.CopyToAsync(originalBodyStream);
        }
        catch (Exception exception)
        {
            var errorResponse = await handler.HandleAsync(context, exception, CancellationToken.None);
            handler.LogException(context, exception);

            using var exceptionStream = new MemoryStream();
            context.Response.Body = exceptionStream;
            await context.Response.WriteAsJsonAsync(errorResponse);
            exceptionStream.Position = 0;
            await exceptionStream.CopyToAsync(originalBodyStream);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}
