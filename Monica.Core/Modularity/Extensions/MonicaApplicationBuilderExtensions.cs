using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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

        var application = app.ApplicationServices.GetRequiredService<MonicaApplication>();
        application.Modules.BeginApplicationPipeline(app);
        ConfigureMonicaHttpListener(app, application.ModuleSystem);
        application.Modules.ConfigApplicationPipeline(app, ModuleOrder.MIDDLEWARE_USE_ROUTING, afterGivenOrder: false);
        app.UseRouting();
        app.UseMonicaEndpointPortGuard(application.ModuleSystem);
        application.Modules.ConfigApplicationPipeline(app, ModuleOrder.MIDDLEWARE_USE_ROUTING, afterGivenOrder: true);
        application.Modules.CompleteApplicationPipeline();
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

        var application = app.ApplicationServices.GetRequiredService<MonicaApplication>();
        application.Modules.BeginEndpointMapping(app);
        application.Modules.ConfigEndpoints(app);
        return app;
    }

    private static void UseMonicaEndpointPortGuard(
        this IApplicationBuilder app,
        IMonicaModuleSystemOptions moduleSystem)
    {
        var port = moduleSystem.MonicaEndpointPort;
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

    private static void ConfigureMonicaHttpListener(
        IApplicationBuilder app,
        IMonicaModuleSystemOptions moduleSystem)
    {
        var port = moduleSystem.MonicaEndpointPort;
        if (port is null || !moduleSystem.AutoAddMonicaHttpListener || app is not WebApplication webApplication)
        {
            return;
        }

        var hasExistingUrls = webApplication.Urls.Count > 0;
        var monicaUrl = BuildMonicaHttpUrl(port.Value, webApplication.Urls, moduleSystem.MonicaEndpointHost);
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

    private static string BuildMonicaHttpUrl(
        int port,
        ICollection<string> existingUrls,
        string? configuredHost)
    {
        var host = ResolveMonicaEndpointHost(existingUrls, configuredHost);
        return $"http://{host}:{port}";
    }

    private static string ResolveMonicaEndpointHost(
        IEnumerable<string> existingUrls,
        string? configuredHost)
    {
        if (!string.IsNullOrWhiteSpace(configuredHost))
        {
            return configuredHost.Trim();
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
