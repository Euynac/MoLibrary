using System.Net.Http.Headers;

namespace Monica.Core.Results.Internal;

internal sealed class ResultEnvelopeHttpRequestInfo
{
    public string? RequestUri { get; init; }

    public string? Method { get; init; }

    public Dictionary<string, string[]>? Headers { get; init; }

    public Dictionary<string, string[]>? ContentHeaders { get; init; }

    public string? RequestMessage { get; init; }

    public string? Content { get; init; }

    public static async Task<ResultEnvelopeHttpRequestInfo?> CreateAsync(HttpRequestMessage? request)
    {
        if (request is null)
        {
            return null;
        }

        var content = request.Content is { } httpContent
            ? await httpContent.ReadAsStringAsync()
            : null;

        return new ResultEnvelopeHttpRequestInfo
        {
            RequestUri = request.RequestUri?.ToString(),
            Method = request.Method.Method,
            Headers = ToHeaderDictionary(request.Headers),
            ContentHeaders = ToHeaderDictionary(request.Content?.Headers),
            RequestMessage = request.ToString(),
            Content = content
        };
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
