using System.Text.Json;

namespace Monica.Guide;

/// <summary>Loads the small public server configuration before application composition.</summary>
public static class GuideProductConfiguration
{
    /// <summary>
    /// Loads the persisted serve port for one product. Products without a loopback server have
    /// no configuration file; they resolve to their definition's default port.
    /// </summary>
    public static GuideServerConfiguration Load(AgentProductDefinition definition, AgentProductPaths? paths = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!definition.ServesLoopback)
        {
            return new GuideServerConfiguration(
                GuideServerConfiguration.CurrentSchemaVersion,
                definition.DefaultPort!.Value);
        }

        paths ??= AgentProductPaths.ForCurrentUser(definition);
        if (!File.Exists(paths.ServerConfigurationFile))
        {
            return new GuideServerConfiguration(
                GuideServerConfiguration.CurrentSchemaVersion,
                definition.DefaultPort!.Value);
        }

        var configuration = JsonSerializer.Deserialize<GuideServerConfiguration>(
                                File.ReadAllBytes(paths.ServerConfigurationFile),
                                GuidePlanning.JsonOptions)
                            ?? throw new InvalidDataException("The product server configuration is empty.");
        if (configuration.SchemaVersion != GuideServerConfiguration.CurrentSchemaVersion)
        {
            throw new InvalidDataException($"Unsupported server configuration schema {configuration.SchemaVersion}.");
        }

        if (configuration.Port is < 1 or > 65535)
        {
            throw new InvalidDataException($"Invalid product server port {configuration.Port}.");
        }

        return configuration;
    }
}
