using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Test.Monica.Configuration.Services.Support;

internal sealed class PassThroughSensitiveValueProtector : IConfigurationSensitiveValueProtector
{
    public ConfigurationStoredValue Protect(ConfigurationStoredValue value)
    {
        return value;
    }

    public ConfigurationStoredValue Unprotect(ConfigurationStoredValue value)
    {
        return value;
    }
}
