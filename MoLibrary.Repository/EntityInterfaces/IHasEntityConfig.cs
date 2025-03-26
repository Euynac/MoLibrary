using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MoLibrary.Repository.EntityInterfaces;

/// <summary>
///    A standard interface for configuring entities.
/// </summary>
/// <typeparam name="TEntity"></typeparam>
[Obsolete("‘›Œ¥ µœ÷")]
public interface IHasEntityConfig<TEntity> where TEntity : MoEntity
{
    /// <summary>
    ///     Configures the entity of type <typeparamref name="TEntity" />.
    /// </summary>
    /// <param name="builder">The builder to be used to configure the entity type.</param>
    void Configure(EntityTypeBuilder<TEntity> builder);
}