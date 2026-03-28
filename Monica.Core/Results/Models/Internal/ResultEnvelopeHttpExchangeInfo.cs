using System.Text.Json;

namespace Monica.Core.Results.Internal;

internal sealed class ResultEnvelopeHttpExchangeInfo
{
    public ResultEnvelopeHttpRequestInfo? Request { get; init; }

    public ResultEnvelopeHttpResponseInfo Response { get; init; } = new();

    public static async Task<ResultEnvelopeHttpExchangeInfo> CreateAsync<TResponse>(
        HttpResponseMessage response,
        string responseContent,
        TResponse? parsedResponse,
        JsonSerializerOptions serializerOptions)
        where TResponse : class, IResultEnvelope
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(serializerOptions);

        return new ResultEnvelopeHttpExchangeInfo
        {
            Request = await ResultEnvelopeHttpRequestInfo.CreateAsync(response.RequestMessage),
            Response = ResultEnvelopeHttpResponseInfo.Create(response, responseContent, parsedResponse, serializerOptions)
        };
    }
}
