using System.Text.Json;

namespace Monica.Core.Results.Internal;

internal sealed class ResultEnvelopeHttpExchangeInfo
{
    public ResultEnvelopeHttpRequestInfo? Request { get; init; }

    public ResultEnvelopeHttpResponseInfo Response { get; init; } = new();

    public static async Task<ResultEnvelopeHttpExchangeInfo> CreateAsync<TResponse>(
        HttpResponseMessage response,
        ResultEnvelopeCapturedContent responseContent,
        TResponse? parsedResponse,
        JsonSerializerOptions serializerOptions,
        int maxBodyBytes)
        where TResponse : class, IResultEnvelope
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(serializerOptions);

        return new ResultEnvelopeHttpExchangeInfo
        {
            Request = await ResultEnvelopeHttpRequestInfo.CreateAsync(response.RequestMessage, maxBodyBytes),
            Response = ResultEnvelopeHttpResponseInfo.Create(response, responseContent, parsedResponse, serializerOptions)
        };
    }
}
