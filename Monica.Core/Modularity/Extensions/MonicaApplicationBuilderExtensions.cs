using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Monica.Core;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Services;

namespace Monica.Core.Modularity.Extensions;

public static class MonicaApplicationBuilderExtensions
{
    /// <summary>
    /// Configures the Monica module middleware pipeline around <c>UseRouting()</c>.
    /// Call this after <c>builder.Build()</c> and before <see cref="MapMonica(IApplicationBuilder)"/>.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder instance.</returns>
    public static IApplicationBuilder UseMonica(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        ConfigureMonicaHttpListener(app);
        ModuleRegistry.ConfigApplicationPipeline(app, ModuleOrder.MIDDLEWARE_USE_ROUTING, afterGivenOrder: false);
        app.UseRouting();
        app.UseMonicaEndpointPortGuard();
        ModuleRegistry.ConfigApplicationPipeline(app, ModuleOrder.MIDDLEWARE_USE_ROUTING, afterGivenOrder: true);
        return app;
    }

    /// <summary>
    /// Configures Monica module endpoint mappings. Call this after <see cref="UseMonica(IApplicationBuilder)"/>.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same application builder instance.</returns>
    public static IApplicationBuilder MapMonica(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        ModuleRegistry.ConfigEndpoints(app);
        return app;
    }

    private static void UseMonicaEndpointPortGuard(this IApplicationBuilder app)
    {
        var port = Mo.ModuleSystem.MonicaEndpointPort;
        if (port is null)
        {
            return;
        }

        app.Use(async (context, next) =>
        {
            var endpoint = context.GetEndpoint();
            if (endpoint?.Metadata.GetMetadata<MonicaEndpointMetadata>() is not null &&
                context.Connection.LocalPort != port.Value)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await next(context);
        });
    }

    private static void ConfigureMonicaHttpListener(IApplicationBuilder app)
    {
        var port = Mo.ModuleSystem.MonicaEndpointPort;
        if (port is null || !Mo.ModuleSystem.AutoAddMonicaHttpListener || app is not WebApplication webApplication)
        {
            return;
        }

        var hasExistingUrls = webApplication.Urls.Count > 0;
        var monicaUrl = BuildMonicaHttpUrl(port.Value, webApplication.Urls);
        if (webApplication.Urls.Any(url => string.Equals(url, monicaUrl, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (!hasExistingUrls)
        {
            webApplication.Urls.Add("http://localhost:5000");
        }

        webApplication.Urls.Add(monicaUrl);
    }

    private static string BuildMonicaHttpUrl(int port, ICollection<string> existingUrls)
    {
        var host = ResolveMonicaEndpointHost(existingUrls);
        return $"http://{host}:{port}";
    }

    private static string ResolveMonicaEndpointHost(IEnumerable<string> existingUrls)
    {
        if (!string.IsNullOrWhiteSpace(Mo.ModuleSystem.MonicaEndpointHost))
        {
            return Mo.ModuleSystem.MonicaEndpointHost.Trim();
        }

        foreach (var existingUrl in existingUrls)
        {
            if (TryGetHost(existingUrl, out var host, out var scheme) &&
                string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                return host;
            }
        }

        foreach (var existingUrl in existingUrls)
        {
            if (TryGetHost(existingUrl, out var host, out _))
            {
                return host;
            }
        }

        return "localhost";
    }

    private static bool TryGetHost(string url, out string host, out string? scheme)
    {
        host = string.Empty;
        scheme = null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        scheme = uri.Scheme;
        host = uri.Host;
        if (!string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        var schemeSeparatorIndex = url.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparatorIndex < 0)
        {
            return false;
        }

        var hostAndPort = url[(schemeSeparatorIndex + 3)..];
        var pathIndex = hostAndPort.IndexOf('/', StringComparison.Ordinal);
        if (pathIndex >= 0)
        {
            hostAndPort = hostAndPort[..pathIndex];
        }

        var portSeparatorIndex = hostAndPort.LastIndexOf(':');
        host = portSeparatorIndex > 0 ? hostAndPort[..portSeparatorIndex] : hostAndPort;
        return !string.IsNullOrWhiteSpace(host);
    }
}
