using Monica.Configuration.Models;

namespace Monica.Configuration.Services.Support;

/// <summary>
/// Coordinates mutation-time validation.
/// </summary>
internal sealed class ConfigurationValidationCoordinator
{
    /// <summary>
    /// Validates a mutation against the current Phase 1 schema constraints.
    /// </summary>
    /// <param name="definition">The target definition.</param>
    /// <param name="request">The mutation request.</param>
    public void Validate(ConfigurationDefinition definition, ConfigurationMutationRequest request)
    {
        if (request.ExpectedSchemaVersion > 0 && request.ExpectedSchemaVersion != definition.SchemaVersion)
        {
            throw new Exceptions.ConfigurationSchemaMismatchException(
                $"Expected schema version {request.ExpectedSchemaVersion}, but definition '{definition.DefinitionKey}' is version {definition.SchemaVersion}.");
        }
    }
}
