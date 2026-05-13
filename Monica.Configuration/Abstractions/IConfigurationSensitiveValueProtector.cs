using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Protects sensitive configuration payloads before persistence and restores them before final binding.
/// </summary>
public interface IConfigurationSensitiveValueProtector
{
    /// <summary>
    /// Protects a plain stored value.
    /// </summary>
    /// <param name="value">The plain value.</param>
    /// <returns>The protected value.</returns>
    ConfigurationStoredValue Protect(ConfigurationStoredValue value);

    /// <summary>
    /// Restores a protected stored value.
    /// </summary>
    /// <param name="value">The protected value.</param>
    /// <returns>The restored value.</returns>
    ConfigurationStoredValue Unprotect(ConfigurationStoredValue value);
}
