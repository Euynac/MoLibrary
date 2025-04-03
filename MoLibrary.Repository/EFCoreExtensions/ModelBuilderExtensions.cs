using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoLibrary.Repository.EntityInterfaces;
using System.Reflection;

namespace MoLibrary.Repository.EFCoreExtensions;

/// <summary>
/// Extension methods for ModelBuilder to support self-configuring entities.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies configurations from entities that implement IHasEntityConfig.
    /// This enables entities to configure themselves rather than requiring separate
    /// configuration classes or configuration within DbContext.
    /// </summary>
    /// <param name="modelBuilder">The model builder instance</param>
    /// <param name="logger">Optional logger to log information or errors</param>
    /// <returns>The same model builder instance</returns>
    public static ModelBuilder ApplyEntityConfigurations(this ModelBuilder modelBuilder, ILogger? logger = null)
    {
        // Get all entity types from the model
        var entityTypes = modelBuilder.Model.GetEntityTypes();

        foreach (var entityType in entityTypes)
        {
            var clrType = entityType.ClrType;
            
            try
            {
                // Skip if not a MoEntity
                if (!clrType.IsAssignableTo(typeof(MoEntity)))
                {
                    continue;
                }
                
                // Find the IHasEntityConfig<TEntity> interface on the entity
                var entityConfigInterface = clrType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && 
                                        i.GetGenericTypeDefinition() == typeof(IHasEntityConfig<>) &&
                                        i.GetGenericArguments()[0] == clrType);
                
                if (entityConfigInterface == null)
                {
                    continue;
                }
                
                // Entity implements IHasEntityConfig<TEntity>
                logger?.LogDebug("Applying configuration from entity class: {EntityType}", clrType.FullName);
                
                // Get the Configure method from the interface
                var configureMethod = entityConfigInterface.GetMethod("Configure");
                if (configureMethod == null)
                {
                    logger?.LogWarning("Configure method not found on entity class: {EntityType}", clrType.FullName);
                    continue;
                }
                
                // Get the EntityTypeBuilder for this entity
                var entityTypeBuilder = modelBuilder.Entity(clrType);
                
                // Try to create an instance unless the class is abstract
                if (clrType.IsAbstract)
                {
                    logger?.LogDebug("Skipping configuration for abstract entity: {EntityType}", clrType.FullName);
                    continue;
                }
                
                object? entity;
                try
                {
                    // Try to create instance using default constructor
                    entity = Activator.CreateInstance(clrType);
                }
                catch (MissingMethodException)
                {
                    // Try to find a constructor with parameters that have default values
                    var constructors = clrType.GetConstructors();
                    var defaultCtor = constructors.FirstOrDefault(c => 
                        c.GetParameters().All(p => p.HasDefaultValue));
                    
                    if (defaultCtor == null)
                    {
                        logger?.LogWarning("Cannot create instance of {EntityType}. No suitable constructor found.", clrType.FullName);
                        continue;
                    }
                    
                    // Create with default parameter values
                    var parameters = defaultCtor.GetParameters()
                        .Select(p => p.DefaultValue)
                        .ToArray();
                    
                    entity = defaultCtor.Invoke(parameters);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to create instance of {EntityType}", clrType.FullName);
                    continue;
                }
                
                if (entity == null)
                {
                    logger?.LogWarning("Failed to create instance of {EntityType}", clrType.FullName);
                    continue;
                }
                
                // Call the Configure method on the entity instance
                try
                {
                    configureMethod.Invoke(entity, new object[] { entityTypeBuilder });
                    logger?.LogDebug("Successfully applied configuration from entity: {EntityType}", clrType.FullName);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Error applying configuration from entity: {EntityType}", clrType.FullName);
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Unexpected error while configuring entity: {EntityType}", clrType.FullName);
            }
        }

        return modelBuilder;
    }
} 