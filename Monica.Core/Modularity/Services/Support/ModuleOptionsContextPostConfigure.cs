using Microsoft.Extensions.Options;
using Monica.Core.Modularity.Abstractions;

namespace Monica.Core.Modularity.Services.Support;

/// <summary>
/// Binds options materialized by Microsoft.Extensions.Options to the Monica host that registered them.
/// </summary>
/// <typeparam name="TOptions">The module option type.</typeparam>
internal sealed class ModuleOptionsContextPostConfigure<TOptions>(MonicaApplication application)
    : IPostConfigureOptions<TOptions>
    where TOptions : class
{
    /// <inheritdoc />
    public void PostConfigure(string? name, TOptions options)
    {
        if (options is IModuleOptionsContext context)
        {
            context.Bind(application);
        }
    }
}
