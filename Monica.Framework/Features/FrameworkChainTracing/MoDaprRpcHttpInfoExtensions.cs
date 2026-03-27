using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core.ExceptionHandling.Abstractions;
using Monica.Core.Features.MoChainTracing;
using Monica.Core.Features.MoChainTracing.Models;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.DomainDrivenDesign.AutoController.MoRpc;
using Monica.Tool.MoResponse;

namespace Monica.Framework.Features.FrameworkChainTracing;


public static class MoDaprRpcHttpInfoExtensions
{
    public static void AddMoDaprGrpcApiHttpInfo(this IServiceCollection services)
    {
        services.AddTransient<MoRpcApiHttpInfoMiddleware>();
    }

    public static void UseMoDaprGrpcApiHttpInfo(this IApplicationBuilder builder)
    {
        builder.UseMiddleware<MoRpcApiHttpInfoMiddleware>();
    }
}


/// <summary>
/// Extends Dapr actor HTTP processing with custom headers and chain tracing.
/// </summary>
internal sealed class MoRpcApiHttpInfoMiddleware(IJsonSerializerOptionsProvider jsonSerializerOptionsProvider, IExceptionHandlerService handler, IMoChainTracing tracing) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.GetEndpoint()?.DisplayName != "Dapr Actors Invoke")
        {
            await next(context);
            return;
        }

        // Ensure the request body can be read multiple times
        context.Request.EnableBuffering();

        if (context.Request.Body.CanRead)
        {
            try
            {
                // //// Leave stream open so next middleware can read it
                // using var reader = new StreamReader(
                //     context.Request.Body,
                //     Encoding.UTF8,
                //     detectEncodingFromByteOrderMarks: false,
                //     bufferSize: 512, leaveOpen: true);
                // var requestBody = await reader.ReadToEndAsync();
                // //// Reset stream position, so next middleware can read it
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

        // Swap out stream with one that is buffered and supports seeking
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;
        try
        {
            using var scope = tracing.BeginScope(context.Request.Path.Value ?? "Actor", "Actor");
            // Hand over to the next middleware and wait for the call to return
            await next(context);

            memoryStream.Position = 0;
            var jsonNode = await JsonSerializer.DeserializeAsync<JsonNode>(memoryStream, jsonSerializerOptionsProvider.SerializerOptions);

            scope.EndWithSuccess();

            if (tracing.GetCurrentChain() is { } chain)
            {
                chain.MarkComplete();

                if (jsonNode is JsonObject jsonObject)
                {
                    var extraInfoKey = jsonSerializerOptionsProvider.UsingJsonNamePolicy(nameof(Res.ExtraInfo));
                    var chainKey = jsonSerializerOptionsProvider.UsingJsonNamePolicy(MoChainContext.CHAIN_KEY);

                    if (!jsonObject.ContainsKey(extraInfoKey) || jsonObject[extraInfoKey] is not JsonObject)
                    {
                        jsonObject[extraInfoKey] = new JsonObject();
                    }

                    var extraInfoObj = jsonObject[extraInfoKey]!.AsObject();
                    extraInfoObj[chainKey] = JsonSerializer.SerializeToNode(chain.Root, jsonSerializerOptionsProvider.SerializerOptions);
                }
            }

            using var newResStream = new MemoryStream();
            context.Response.Body = newResStream;

            await context.Response.WriteAsJsonAsync(jsonNode, jsonSerializerOptionsProvider.SerializerOptions);

            // Copy body back to so its available to the user agent
            newResStream.Position = 0;
            await newResStream.CopyToAsync(originalBodyStream);
        }
        catch (Exception e)
        {
            var exRes = await handler.HandleAsync(context, e, CancellationToken.None);
            handler.LogException(context, e);
            using var exceptionStream = new MemoryStream();
            context.Response.Body = exceptionStream;
            await context.Response.WriteAsJsonAsync(exRes);
            exceptionStream.Position = 0;
            await exceptionStream.CopyToAsync(originalBodyStream);
            // The original HTTP response stream must be restored because the temporary stream is disposed here.

        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}
