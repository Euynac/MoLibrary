using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Monica.ProjectUnits.Abstractions;
using Monica.ProjectUnits.Services.Support;

namespace Monica.ProjectUnits.Providers.AspNetCore;

internal static class RequestMonitorExtensions
{
    internal static void AddRequestFilter(this IServiceCollection services)
    {
        services.AddSingleton<IRequestFilter>(serviceProvider =>
            serviceProvider.GetRequiredService<ProjectUnitCatalog>());
        services.AddSingleton<RequestFilterMiddleware>();
    }

    internal static void UseRequestFilter(this IApplicationBuilder builder)
    {
        builder.UseMiddleware<RequestFilterMiddleware>();
    }
}

/// <summary>
/// Stops requests whose paths have been disabled through the host's <see cref="IRequestFilter"/>.
/// </summary>
internal sealed class RequestFilterMiddleware(ProjectUnitCatalog catalog) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (catalog.IsRequestDisabled(context.Request.Path.Value ?? string.Empty))
        {
            return;
        }

        await next(context);
    }
}
