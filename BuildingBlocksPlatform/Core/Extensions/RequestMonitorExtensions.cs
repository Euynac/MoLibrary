using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.Core.Extensions;

public static class RequestMonitorExtensions
{
    internal static void AddRequestFilter(this IServiceCollection services)
    {
        services.AddSingleton<RequestFilterMiddleware>();
        services.AddSingleton<IRequestFilter, RequestFilterMiddleware>();
    }
   
    internal static void UseRequestFilter(this IApplicationBuilder builder)
    {
        builder.UseMiddleware<RequestFilterMiddleware>();
    }
}

public interface IRequestFilter
{
    public void Disable(string url);
    public void Enable(string url);
    public List<string> GetDisabledUrls();
}


/// <summary>
/// Middleware for monitor request.
/// </summary>
public class RequestFilterMiddleware : IMiddleware, IRequestFilter
{
    private static readonly HashSet<string> _excludedUrls = [];
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if(_excludedUrls.Contains(context.Request.Path)) return;
        await next(context);
    }

    public void Disable(string url)
    {
        _excludedUrls.Add(url);
    }

    public void Enable(string url)
    {
        _excludedUrls.Remove(url);
    }

    public List<string> GetDisabledUrls()
    {
        return [.. _excludedUrls];
    }
}