using System.Net.Http.Headers;
using System.Text.Json;
using Monica.Tool.Extensions;

namespace Monica.Core.Results.Internal;

internal sealed class ResultEnvelopeHttpResponseInfo
{
    public string ResponseType { get; init; } = string.Empty;

    public object? Envelope { get; init; }

    public object? BodyJson { get; init; }

    public string RawContent { get; init; } = string.Empty;

    public bool IsContentTruncated { get; init; }

    public Dictionary<string, string[]>? Headers { get; init; }

    public Dictionary<string, string[]>? ContentHeaders { get; init; }

    public int StatusCode { get; init; }

    public string? ReasonPhrase { get; init; }

    public bool IsSuccessStatusCode { get; init; }

    public static ResultEnvelopeHttpResponseInfo Create<TResponse>(
        HttpResponseMessage response,
        ResultEnvelopeCapturedContent responseContent,
        TResponse? parsedResponse,
        JsonSerializerOptions serializerOptions)
        where TResponse : class, IResultEnvelope
    {
        ArgumentNullException.ThrowIfNull(response);

        return new ResultEnvelopeHttpResponseInfo
        {
            ResponseType = typeof(TResponse).GetCleanFullName(),
            Envelope = parsedResponse,
            BodyJson = parsedResponse is not null ? null : TryDeserializeBody(responseContent.Content, serializerOptions),
            RawContent = responseContent.DisplayContent,
            IsContentTruncated = responseContent.IsTruncated,
            Headers = ToHeaderDictionary(response.Headers),
            ContentHeaders = ToHeaderDictionary(response.Content?.Headers),
            StatusCode = (int)response.StatusCode,
            ReasonPhrase = response.ReasonPhrase,
            IsSuccessStatusCode = response.IsSuccessStatusCode
        };
    }

    public static ResultEnvelopeHttpResponseInfo FromRawContent(string rawContent)
    {
        return new ResultEnvelopeHttpResponseInfo
        {
            RawContent = string.IsNullOrWhiteSpace(rawContent) ? "<Empty>" : rawContent
        };
    }

    private static object TryDeserializeBody(string responseContent, JsonSerializerOptions serializerOptions)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return "<Empty>";
        }

        try
        {
            return JsonSerializer.Deserialize<object>(responseContent, serializerOptions) ?? responseContent;
        }
        catch
        {
            return responseContent;
        }
    }

    private static Dictionary<string, string[]>? ToHeaderDictionary(HttpHeaders? headers)
    {
        if (headers is null)
        {
            return null;
        }

        var values = headers
            .Select(header => new KeyValuePair<string, string[]>(header.Key, header.Value.ToArray()))
            .ToArray();

        return values.Length == 0 ? null : values.ToDictionary(static entry => entry.Key, static entry => entry.Value);
    }
}
