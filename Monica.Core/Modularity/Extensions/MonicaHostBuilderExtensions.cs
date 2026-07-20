using Microsoft.Extensions.Hosting;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Services;

namespace Monica.Core.Modularity.Extensions;

/// <summary>
/// Creates a host-bound Monica module composition.
/// </summary>
public static class MonicaHostBuilderExtensions
{
    private static readonly object MONICA_APPLICATION_KEY = new();

    /// <summary>
    /// Composes, validates, and registers Monica modules for one host.
    /// </summary>
    /// <typeparam name="TBuilder">The concrete host builder type.</typeparam>
    /// <param name="builder">The host application builder that owns the composition.</param>
    /// <param name="configure">A synchronous callback that configures application defaults and modules.</param>
    /// <returns>The same host builder instance.</returns>
    /// <remarks>
    /// Call this exactly once before <c>Build()</c>. The module graph is sealed when the callback returns;
    /// retained module guides reject later mutation.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when Monica was already added to this host builder.</exception>
    public static TBuilder AddMonica<TBuilder>(
        this TBuilder builder,
        Action<IMonicaBuilder> configure)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        if (builder.Properties.ContainsKey(MONICA_APPLICATION_KEY))
        {
            throw new InvalidOperationException("AddMonica(...) can be called only once for a host builder.");
        }

        var application = new MonicaApplication();
        var monicaBuilder = new MonicaBuilder(builder, application);
        builder.Properties.Add(MONICA_APPLICATION_KEY, application);

        try
        {
            configure(monicaBuilder);
            monicaBuilder.Complete();
            return builder;
        }
        catch
        {
            builder.Properties.Remove(MONICA_APPLICATION_KEY);
            application.Dispose();
            throw;
        }
    }
}
