using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Placeholder sensitive value protector. Phase 4 will replace this with IDataProtection-backed payloads.
/// </summary>
internal sealed class ConfigurationSensitiveValueProtector : IConfigurationSensitiveValueProtector
{
    /// <inheritdoc />
    public ConfigurationStoredValue Protect(ConfigurationStoredValue value)
    {
        return value;
    }

    /// <inheritdoc />
    public ConfigurationStoredValue Unprotect(ConfigurationStoredValue value)
    {
        return value;
    }
}
