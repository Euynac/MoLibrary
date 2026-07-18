using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Monica.Configuration.Models;
using Monica.Modules;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Resolves stable logical-publisher identity separately from process-level audit identity.
/// </summary>
internal sealed class ConfigurationPublisherIdentityProvider(
    IHostEnvironment hostEnvironment,
    IOptions<ModuleConfigurationOption> options)
{
    private ConfigurationPublisherIdentity? _identity;

    internal ConfigurationPublisherIdentity GetIdentity()
    {
        return _identity ??= CreateIdentity();
    }

    private ConfigurationPublisherIdentity CreateIdentity()
    {
        var entryAssembly = Assembly.GetEntryAssembly();
        var publisherKey = FirstNonEmpty(
            options.Value.PublisherKey,
            hostEnvironment.ApplicationName,
            entryAssembly?.GetName().Name);
        var instanceId = FirstNonEmpty(
            Environment.GetEnvironmentVariable("MONICA_CONFIGURATION_INSTANCE_ID"),
            options.Value.InstanceId);
        var publisherName = FirstNonEmpty(
            Environment.GetEnvironmentVariable("MONICA_CONFIGURATION_INSTANCE_NAME"),
            Environment.GetEnvironmentVariable("HOSTNAME"),
            Environment.MachineName);

        return new ConfigurationPublisherIdentity
        {
            PublisherKey = publisherKey,
            InstanceId = instanceId,
            Name = publisherName,
            Version = entryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? entryAssembly?.GetName().Version?.ToString()
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim()
               ?? throw new InvalidOperationException("Configuration publisher identity could not be resolved.");
    }
}
