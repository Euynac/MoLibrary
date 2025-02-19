
namespace BuildingBlocksPlatform.Repository.EntityInterfaces.Auditing;

/// <summary>
/// A standard interface to add DeletionTime property to a class.
/// It also makes the class soft delete (see <see cref="IHasSoftDelete"/>).
/// </summary>
public interface IHasDeletionTime : IHasSoftDelete
{
    /// <summary>
    /// Deletion time.
    /// </summary>
    DateTime? DeletionTime { get; }
}
