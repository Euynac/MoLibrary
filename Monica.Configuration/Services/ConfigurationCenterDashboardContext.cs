using Monica.Configuration.Abstractions;
using Monica.Configuration.Models;

namespace Monica.Configuration.Services;

internal sealed class ConfigurationCenterDashboardContext : IConfigurationDashboardContext
{
    public ConfigurationDashboardMode Mode => ConfigurationDashboardMode.Center;
}
