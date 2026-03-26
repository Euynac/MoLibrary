using System.Linq;
using Microsoft.AspNetCore.Http;

namespace Monica.Core.ExceptionHandling.Models;

internal sealed class HttpRequestSnapshot
{
    public string Method { get; set; } = string.Empty;

    public string Scheme { get; set; } = string.Empty;

    public bool IsHttps { get; set; }

    public string Host { get; set; } = string.Empty;

    public string PathBase { get; set; } = string.Empty;

    public string? Path { get; set; }

    public string? QueryString { get; set; }

    public Dictionary<string, string[]>? Query { get; set; }

    public string? Protocol { get; set; }

    public Dictionary<string, string[]>? Headers { get; init; }

    public Dictionary<string, string>? Cookies { get; set; }

    public long? ContentLength { get; set; }

    public string? ContentType { get; set; }

    public Dictionary<string, string?>? RouteValues { get; set; }

    public static HttpRequestSnapshot? Create(HttpRequest? request)
    {
        if (request == null)
        {
            return null;
        }

        return new HttpRequestSnapshot
        {
            Method = request.Method,
            Scheme = request.Scheme,
            IsHttps = request.IsHttps,
            Host = request.Host.Value ?? string.Empty,
            PathBase = request.PathBase.Value ?? string.Empty,
            Path = request.Path.Value,
            QueryString = request.QueryString.Value,
            Query = request.Query.Count == 0
                ? null
                : request.Query.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(static value => value ?? string.Empty).ToArray()),
            Protocol = request.Protocol,
            Headers = request.Headers.Count == 0
                ? null
                : request.Headers.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(static value => value ?? string.Empty).ToArray()),
            Cookies = request.Cookies.Count == 0
                ? null
                : request.Cookies.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            ContentLength = request.ContentLength,
            ContentType = request.ContentType,
            RouteValues = request.RouteValues.Count == 0
                ? null
                : request.RouteValues.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString())
        };
    }
}
