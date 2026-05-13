using Microsoft.AspNetCore.DataProtection;
using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Protects sensitive configuration values with ASP.NET Core Data Protection.
/// </summary>
internal sealed class ConfigurationSensitiveValueProtector : IConfigurationSensitiveValueProtector
{
    private const string PROTECTOR_PURPOSE = "Monica.Configuration.SensitiveValue.v1";

    private readonly IDataProtector _protector;

    /// <summary>
    /// Creates a sensitive value protector.
    /// </summary>
    /// <param name="provider">The root data protection provider.</param>
    public ConfigurationSensitiveValueProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(PROTECTOR_PURPOSE);
    }

    /// <inheritdoc />
    public ConfigurationStoredValue Protect(ConfigurationStoredValue value)
    {
        if (value.Kind != ConfigurationStoredValueKind.PlainJson || value.PlainJson is null)
        {
            return value;
        }

        return new ConfigurationStoredValue
        {
            Kind = ConfigurationStoredValueKind.ProtectedJson,
            ProtectedPayload = _protector.Protect(value.PlainJson)
        };
    }

    /// <inheritdoc />
    public ConfigurationStoredValue Unprotect(ConfigurationStoredValue value)
    {
        if (value.Kind != ConfigurationStoredValueKind.ProtectedJson)
        {
            return value;
        }

        if (string.IsNullOrWhiteSpace(value.ProtectedPayload))
        {
            return ConfigurationStoredValue.Null;
        }

        return ConfigurationStoredValue.Plain(_protector.Unprotect(value.ProtectedPayload));
    }
}
