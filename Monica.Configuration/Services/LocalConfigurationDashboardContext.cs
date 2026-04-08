using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services;

internal sealed class LocalConfigurationDashboardContext : IConfigurationDashboardContext
{
    public ConfigurationDashboardMode Mode => ConfigurationDashboardMode.Local;
}
