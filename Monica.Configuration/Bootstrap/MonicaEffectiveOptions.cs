using Microsoft.Extensions.Configuration;

namespace Monica.Configuration.Bootstrap;

/// <summary>
/// Creates readers for Monica effective options before the application service provider is available.
/// </summary>
public static class MonicaEffectiveOptions
{
    /// <summary>
    /// Creates a builder for a startup effective-options reader.
    /// </summary>
    /// <param name="bootstrapConfiguration">
    /// The host bootstrap configuration used to seed missing effective-value documents.
    /// </param>
    /// <param name="configure">Optional reader configuration.</param>
    /// <returns>The reader builder.</returns>
    public static MonicaEffectiveOptionsReaderBuilder CreateReader(
        IConfiguration bootstrapConfiguration,
        Action<MonicaEffectiveOptionsReaderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(bootstrapConfiguration);

        var options = new MonicaEffectiveOptionsReaderOptions();
        configure?.Invoke(options);
        return new MonicaEffectiveOptionsReaderBuilder(bootstrapConfiguration, options);
    }
}
