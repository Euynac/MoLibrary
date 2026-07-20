using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core.JsonSerialization.Abstractions;
using Monica.Core.Results.Abstractions;
using Monica.Core.Results.Models.Internal;
using Monica.Tool.Diagnostics;
using Monica.Tool.Extensions;
using Monica.Modules;

namespace Monica.Core.Results.Services;

/// <summary>
/// Centralizes Monica result projection, HTTP response shaping, and remote response resolution.
/// </summary>
public sealed class ResultEnvelopeProvider(
    IJsonSerializerOptionsProvider serializerOptionsProvider,
    IOptions<ModuleResultEnvelopeOption> options,
    ILogger<ResultEnvelopeProvider> logger) : IResultEnvelopeReader
{
    private readonly JsonSerializerOptions _serializerOptions = serializerOptionsProvider.SerializerOptions;
    private readonly int _maxDiagnosticBodyBytes = options.Value.MaxRemoteDiagnosticBodyBytes;

    /// <summary>
    /// Converts a Monica result envelope into a Minimal API result.
    /// </summary>
    /// <param name="response">The Monica result envelope.</param>
    /// <returns>The Minimal API result.</returns>
    public static IResult ToMinimalApiResult(IResultEnvelope response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return Microsoft.AspNetCore.Http.Results.Json(
            response,
            statusCode: (int?)response.ToHttpStatusCode());
    }

    /// <summary>
    /// Converts a Monica result envelope into an MVC object result.
    /// </summary>
    /// <param name="response">The Monica result envelope.</param>
    /// <returns>The MVC object result.</returns>
    public static ObjectResult ToMvcResult(IResultEnvelope response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new ObjectResult(response)
        {
            StatusCode = (int?)response.ToHttpStatusCode()
        };
    }

    /// <summary>
    /// Deserializes remote JSON into a Monica result envelope.
    /// </summary>
    /// <typeparam name="TResponse">The Monica envelope type to deserialize.</typeparam>
    /// <param name="json">The remote JSON payload.</param>
    /// <returns>The deserialized Monica result envelope.</returns>
    /// <exception cref="JsonException">Thrown when the payload cannot be deserialized into the requested envelope type.</exception>
    public TResponse DeserializeResponse<TResponse>(string json)
        where TResponse : class, IResultEnvelope, new()
    {
        ArgumentNullException.ThrowIfNull(json);

        return JsonSerializer.Deserialize<TResponse>(json, _serializerOptions)
               ?? throw new JsonException(
                   $"The remote response could not be deserialized into {typeof(TResponse).GetCleanFullName()}.");
    }

    internal async ValueTask<TResponse> DeserializeResponseAsync<TResponse>(
        Stream jsonStream,
        CancellationToken cancellationToken = default)
        where TResponse : class, IResultEnvelope, new()
    {
        ArgumentNullException.ThrowIfNull(jsonStream);

        return await JsonSerializer.DeserializeAsync<TResponse>(jsonStream, _serializerOptions, cancellationToken)
               ?? throw new JsonException(
                   $"The remote response could not be deserialized into {typeof(TResponse).GetCleanFullName()}.");
    }

    /// <summary>
    /// Reads and resolves a remote HTTP response into a Monica result envelope.
    /// </summary>
    /// <typeparam name="TResponse">The Monica envelope type to deserialize.</typeparam>
    /// <param name="httpResponse">The remote HTTP response.</param>
    /// <param name="cancellationToken">A token that cancels response reading and deserialization.</param>
    /// <returns>The resolved Monica envelope, or an internal-error envelope when the remote payload is invalid.</returns>
    public async Task<TResponse> ReadRemoteResponse<TResponse>(
        HttpResponseMessage httpResponse,
        CancellationToken cancellationToken = default)
        where TResponse : class, IResultEnvelope, new()
    {
        ArgumentNullException.ThrowIfNull(httpResponse);

        ResultEnvelopeCapturedContent responseContent;
        TResponse? parsedResponse = null;
        Exception? exception = null;
        Stream? responseStream = null;
        ResultEnvelopeContentCaptureStream? captureStream = null;

        try
        {
            responseStream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);
            var decodedResponseStream = CreateDecodedResponseStream(responseStream, httpResponse.Content.Headers.ContentEncoding);
            captureStream = new ResultEnvelopeContentCaptureStream(decodedResponseStream, _maxDiagnosticBodyBytes);
            parsedResponse = await DeserializeResponseAsync<TResponse>(captureStream, cancellationToken);

            if (parsedResponse.IsRemoteResultHealthy())
            {
                return parsedResponse;
            }

            responseContent = captureStream.ToCapturedContent(httpResponse.Content.Headers);
        }
        catch (Exception ex)
        {
            exception = ex;
            responseContent = captureStream?.ToCapturedContent(httpResponse.Content.Headers) ?? ResultEnvelopeCapturedContent.Empty;
        }
        finally
        {
            if (captureStream is not null)
            {
                await captureStream.DisposeAsync();
            }
            else if (responseStream is not null)
            {
                await responseStream.DisposeAsync();
            }
        }

        var errorResponse = new TResponse
        {
            Status = ResStatus.InternalError,
            Message = "Failed to process remote API response."
        };
        var exchangeInfo = await ResultEnvelopeHttpExchangeInfo.CreateAsync(
            httpResponse,
            responseContent,
            parsedResponse,
            _serializerOptions,
            _maxDiagnosticBodyBytes);

        AppendExceptionMetadata(errorResponse, exception, responseContent);
        AppendExchangeMetadata(errorResponse, exchangeInfo);
        LogRemoteResponseError(logger, exchangeInfo, httpResponse, errorResponse, exception);
        return errorResponse;
    }

    private static Stream CreateDecodedResponseStream(Stream responseStream, ICollection<string>? contentEncodings)
    {
        ArgumentNullException.ThrowIfNull(responseStream);

        if (contentEncodings is null || contentEncodings.Count == 0)
        {
            return responseStream;
        }

        var decodedStream = responseStream;
        foreach (var encoding in contentEncodings.Reverse())
        {
            decodedStream = encoding.Trim().ToLowerInvariant() switch
            {
                "gzip" => new GZipStream(decodedStream, CompressionMode.Decompress),
                "deflate" => new DeflateStream(decodedStream, CompressionMode.Decompress),
                "br" => new BrotliStream(decodedStream, CompressionMode.Decompress),
                "identity" => decodedStream,
                "" => decodedStream,
                _ => throw new NotSupportedException($"Remote response content encoding '{encoding}' is not supported.")
            };
        }

        return decodedStream;
    }

    private static void AppendExchangeMetadata(
        IResultEnvelope errorResponse,
        ResultEnvelopeHttpExchangeInfo exchangeInfo)
    {
        errorResponse.AppendMetadata(ResultEnvelopeMetadataKeys.Response, exchangeInfo.Response);

        if (exchangeInfo.Request is not null)
        {
            errorResponse.AppendMetadata(ResultEnvelopeMetadataKeys.Request, exchangeInfo.Request);
        }
    }

    private static void AppendExceptionMetadata(
        IResultEnvelope errorResponse,
        Exception? exception,
        ResultEnvelopeCapturedContent responseContent)
    {
        if (exception is null)
        {
            return;
        }

        if (exception is JsonException jsonException)
        {
            errorResponse.AppendMetadata(
                ResultEnvelopeMetadataKeys.DeserializationError,
                ResultEnvelopeDeserializationErrorInfo.Create(jsonException, responseContent));
        }
        else
        {
            errorResponse.AppendMetadata(
                ResultEnvelopeMetadataKeys.Exception,
                ResultEnvelopeExceptionInfo.Create(exception));
        }

        var innerException = exception.InnerException;
        while (innerException is not null)
        {
            errorResponse.AppendMetadata(
                ResultEnvelopeMetadataKeys.Exception,
                ResultEnvelopeExceptionInfo.Create(innerException));
            innerException = innerException.InnerException;
        }
    }

    private static void LogRemoteResponseError(
        ILogger logger,
        ResultEnvelopeHttpExchangeInfo exchangeInfo,
        HttpResponseMessage httpResponse,
        IResultEnvelope errorResponse,
        Exception? exception)
    {
        if (exception is null)
        {
            logger.LogError(
                "Failed to process remote API response from {RequestUri}. Exchange: {@Exchange}. Result: {Result}",
                httpResponse.RequestMessage?.RequestUri,
                exchangeInfo,
                errorResponse.ToJsonStringForce());
            return;
        }

        logger.LogError(
            exception,
            "Failed to process remote API response from {RequestUri}. Exchange: {@Exchange}. Result: {Result}",
            httpResponse.RequestMessage?.RequestUri,
            exchangeInfo,
            errorResponse.ToJsonStringForce());
    }
}
