using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MoLibrary.Repository.EntityInterfaces;

/// <summary>
///    A standard interface for configuring entities. Implementing this interface
///    allows an entity to configure itself within the same class file rather than
///    using separate IEntityTypeConfiguration implementations or DbContext configuration.
/// </summary>
/// <typeparam name="TEntity">The entity type being configured</typeparam>
public interface IHasEntityConfig<TEntity> where TEntity : MoEntity
{
    /// <summary>
    ///     Configures the entity of type <typeparamref name="TEntity" />.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity type.</param>
    void Configure(EntityTypeBuilder<TEntity> builder);
}