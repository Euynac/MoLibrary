using Microsoft.AspNetCore.Mvc;
using Monica.WebApi.AutoControllers.Services.Support;

namespace Monica.WebApi.AutoControllers.Extensions;

internal static class MvcOptionsExtensions
{
    public static void ConfigAutoController(this MvcOptions options, IServiceProvider provider)
    {
        AddConventions(options, provider);
        AddActionFilters(options);
    }

    private static void AddConventions(MvcOptions options, IServiceProvider provider)
    {
        options.Conventions.Add(new ServiceConventionWrapper(provider));
    }

    private static void AddActionFilters(MvcOptions options)
    {
        options.Filters.AddService(typeof(ResultEnvelopeMvcFilter));
    }
}
