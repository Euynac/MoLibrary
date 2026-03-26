using System.Linq;
using Microsoft.AspNetCore.Http;

namespace Monica.Core.ExceptionHandling.Models;

internal sealed class HttpResponseSnapshot
{
    public int StatusCode { get; set; }

    public Dictionary<string, string[]>? Headers { get; init; }

    public long? ContentLength { get; set; }

    public string? ContentType { get; set; }

    public static HttpResponseSnapshot? Create(HttpResponse? response)
    {
        if (response == null)
        {
            return null;
        }

        return new HttpResponseSnapshot
        {
            StatusCode = response.StatusCode,
            Headers = response.Headers.Count == 0
                ? null
                : response.Headers.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(static value => value ?? string.Empty).ToArray()),
            ContentLength = response.ContentLength,
            ContentType = response.ContentType
        };
    }
}
