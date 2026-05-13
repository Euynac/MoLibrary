using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Monica.Configuration.Abstractions;
using Monica.Configuration.EfCore.DbContext;
using Monica.Configuration.EfCore.Entities;
using Monica.Configuration.Models;

namespace Monica.Configuration.EfCore.Services;

/// <summary>
/// Publishes owner-scanned definitions into the EF Core schema store.
/// </summary>
public sealed class EfCoreConfigurationDefinitionPublisher(ConfigurationDbContext dbContext)
    : IConfigurationDefinitionPublisher
{
    /// <inheritdoc />
    public async Task PublishAsync(IReadOnlyList<ConfigurationDefinition> definitions, CancellationToken cancellationToken)
    {
        foreach (var definition in definitions)
        {
            var entity = await dbContext.ConfigurationDefinitions
                .FirstOrDefaultAsync(x => x.DefinitionKey == definition.DefinitionKey, cancellationToken);

            if (entity is null)
            {
                entity = new ConfigurationDefinitionEntity { DefinitionKey = definition.DefinitionKey };
                dbContext.ConfigurationDefinitions.Add(entity);
            }

            entity.SectionPath = definition.SectionPath;
            entity.DisplayName = definition.DisplayName;
            entity.ClrTypeName = definition.ClrTypeName;
            entity.OwnerModule = definition.OwnerModule;
            entity.Category = definition.Category;
            entity.SchemaVersion = definition.SchemaVersion;
            entity.SchemaHash = definition.SchemaHash;
            entity.ReloadBehavior = definition.ReloadBehavior.ToString();
            entity.DefinitionJson = JsonSerializer.Serialize(definition);
            entity.LastSeenTime = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
