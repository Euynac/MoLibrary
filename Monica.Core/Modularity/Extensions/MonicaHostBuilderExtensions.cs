using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Services;

namespace Monica.Core.Modularity.Extensions;

public static class MonicaHostBuilderExtensions
{
    /// <summary>
    /// Registers all Monica modules. Call this after all <c>Mo.Add*</c> calls and before <c>builder.Build()</c>.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The same builder instance.</returns>
    public static IHostApplicationBuilder UseMonica(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        ModuleRegistry.RegisterServices(builder);
        return builder;
    }
}
