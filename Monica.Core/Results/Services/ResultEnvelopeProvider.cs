using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Monica.Core.Logging;
using Monica.Core.Results.Internal;
using Monica.Tool.Extensions;
using Monica.Tool.General;

namespace Monica.Core.Results.Services;

/// <summary>
/// Centralizes Monica result projection, HTTP response shaping, and remote response resolution.
/// </summary>
public static class ResultEnvelopeProvider
{
    private static readonly ILogger Logger = LogManager.For(typeof(ResultEnvelopeProvider));

    /// <summary>
    /// Gets the serializer options used for inbound response deserialization.
    /// </summary>
    internal static JsonSerializerOptions SerializerOptions { get; set; } = new();

    /// <summary>
    /// Gets the maximum number of bytes captured for remote request and response diagnostics.
    /// </summary>
    internal static int MaxDiagnosticBodyBytes { get; set; } = 32 * 1024;

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
    public static TResponse DeserializeResponse<TResponse>(string json)
        where TResponse : class, IResultEnvelope, new()
    {
        ArgumentNullException.ThrowIfNull(json);

        return JsonSerializer.Deserialize<TResponse>(json, SerializerOptions)
               ?? throw new JsonException(
                   $"The remote response could not be deserialized into {typeof(TResponse).GetCleanFullName()}.");
    }

    internal static async ValueTask<TResponse> DeserializeResponseAsync<TResponse>(
        Stream jsonStream,
        CancellationToken cancellationToken = default)
        where TResponse : class, IResultEnvelope, new()
    {
        ArgumentNullException.ThrowIfNull(jsonStream);

        return await JsonSerializer.DeserializeAsync<TResponse>(jsonStream, SerializerOptions, cancellationToken)
               ?? throw new JsonException(
                   $"The remote response could not be deserialized into {typeof(TResponse).GetCleanFullName()}.");
    }

    /// <summary>
    /// Reads and resolves a remote HTTP response into a Monica result envelope.
    /// </summary>
    /// <typeparam name="TResponse">The Monica envelope type to deserialize.</typeparam>
    /// <param name="httpResponse">The remote HTTP response.</param>
    /// <param name="logger">The optional logger used for diagnostics.</param>
    /// <returns>The resolved Monica envelope, or an internal-error envelope when the remote payload is invalid.</returns>
    public static async Task<TResponse> ReadRemoteResponse<TResponse>(
        HttpResponseMessage httpResponse,
        ILogger? logger = null)
        where TResponse : class, IResultEnvelope, new()
    {
        ArgumentNullException.ThrowIfNull(httpResponse);

        var responseContent = ResultEnvelopeCapturedContent.Empty;
        TResponse? parsedResponse = null;
        Exception? exception = null;
        Stream? responseStream = null;
        ResultEnvelopeContentCaptureStream? captureStream = null;

        try
        {
            responseStream = await httpResponse.Content.ReadAsStreamAsync();
            captureStream = new ResultEnvelopeContentCaptureStream(responseStream, MaxDiagnosticBodyBytes);
            parsedResponse = await DeserializeResponseAsync<TResponse>(captureStream);

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
            SerializerOptions,
            MaxDiagnosticBodyBytes);

        AppendExceptionMetadata(errorResponse, exception, responseContent);
        AppendExchangeMetadata(errorResponse, exchangeInfo);
        LogRemoteResponseError(logger ?? Logger, exchangeInfo, httpResponse, errorResponse, exception);
        return errorResponse;
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
