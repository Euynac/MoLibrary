using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoLibrary.Repository.EntityInterfaces;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Repository.EFCoreExtensions;

/// <summary>
/// Extension methods for ModelBuilder to support self-configuring entities.
/// </summary>
public static class ModelBuilderExtensions
{
    public static ModelBuilder ApplyConfiguration<TEntity>(ModelBuilder builder,
        IHasEntitySelfConfig<TEntity> configuration) where TEntity : MoEntity, IHasEntitySelfConfig<TEntity>
    {
        configuration.Configure(builder.Entity<TEntity>());
        return builder;
    }

    public static ModelBuilder ApplyEntitySeparateConfigurations(this ModelBuilder modelBuilder, MoRepositoryOptions moOptions,
        ILogger? logger = null)
    {
        if (moOptions.DisableEntitySeparateConfiguration) return modelBuilder;
      
        var entityTypes = modelBuilder.Model.GetEntityTypes().Select(p => p.Name).ToHashSet();

        foreach (var assembly in modelBuilder.Model.GetEntityTypes().Select(p => Assembly.GetAssembly(p.ClrType))
                     .DistinctBy(a => a!.FullName).ToList())
        {
            modelBuilder.ApplyConfigurationsFromAssembly(assembly!, t =>
            {
                if (t.FullName is null) return false;
                foreach (var type in t.GetInterfaces())
                {
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)
                                           && type.GenericTypeArguments.FirstOrDefault() is { FullName: not null } entity
                                           && entityTypes.Contains(entity.FullName))
                    {
                        return true;
                    }
                }

                return false;
            });
        }

        return modelBuilder;
    }

    /// <summary>
    /// Applies configurations from entities that implement IHasEntitySelfConfig.
    /// This enables entities to configure themselves rather than requiring separate
    /// configuration classes or configuration within DbContext.
    /// </summary>
    /// <param name="modelBuilder">The model builder instance</param>
    /// <param name="logger">Optional logger to log information or errors</param>
    /// <param name="moOptions"></param>
    /// <returns>The same model builder instance</returns>
    public static ModelBuilder ApplyEntitySelfConfigurations(this ModelBuilder modelBuilder, MoRepositoryOptions moOptions,
        ILogger? logger = null)
    {
        if (moOptions.DisableEntitySelfConfiguration) return modelBuilder;
        // Get all entity types from the model
        var entityTypes = modelBuilder.Model.GetEntityTypes();

        var methodInfo = typeof(ModelBuilderExtensions).GetMethods().Single(e => e is { Name: nameof(ApplyConfiguration), ContainsGenericParameters: true });

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
                
                // Find the IHasEntitySelfConfig<TEntity> interface on the entity
                var entityConfigInterface = clrType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && 
                                        i.GetGenericTypeDefinition() == typeof(IHasEntitySelfConfig<>) &&
                                        i.GetGenericArguments()[0] == clrType);
                
                if (entityConfigInterface == null)
                {
                    continue;
                }
                
                // Entity implements IHasEntitySelfConfig<TEntity>
                logger?.LogDebug("Applying configuration from entity class: {EntityType}", clrType.FullName);
                
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
                    methodInfo.MakeGenericMethod(clrType).Invoke(null, [modelBuilder, entity]);
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