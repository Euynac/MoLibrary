using Monica.Repository.UnitOfWork.Models;

namespace Monica.Repository.UnitOfWork.Abstractions;
/// <summary>
/// Defines the contract for managing units of work within the application.
/// </summary>
/// <remarks>
/// A unit of work is a design pattern that maintains a list of operations 
/// to be performed within a transactional boundary. This interface provides 
/// methods to begin and manage such units of work.
/// </remarks>
public interface IUnitOfWorkManager
{
    /// <summary>
    /// Gets the current active unit of work, if any.
    /// </summary>
    /// <value>
    /// An instance of <see cref="IUnitOfWork"/> representing the current unit of work,
    /// or <c>null</c> if no unit of work is active.
    /// </value>
    IUnitOfWork? Current { get; }

    /// <summary>
    /// Begins a new unit of work with the specified options.
    /// </summary>
    /// <param name="options">The options to configure the unit of work.</param>
    /// <param name="requiresNew">
    /// A boolean value indicating whether a new unit of work should be created 
    /// even if there is an existing one.
    /// </param>
    /// <remarks>
    /// Ensure the isolation level is configured correctly; otherwise, autoSave and SaveChanges calls made outside the UoW
    /// may not be persisted to the database as expected.
    /// </remarks>
    /// <returns>
    /// An instance of <see cref="IUnitOfWork"/> representing the newly created unit of work.
    /// </returns>
    IUnitOfWork Begin(UnitOfWorkOptions options, bool requiresNew = false);

    /// <summary>
    /// Begins a new unit of work with the specified options.
    /// </summary>
    /// <param name="requiresNew">
    /// A boolean value indicating whether a new unit of work should be created 
    /// even if there is an existing one.
    /// </param>
    /// <returns>
    /// An instance of <see cref="IUnitOfWork"/> representing the newly created unit of work.
    /// </returns>
    IUnitOfWork Begin(bool requiresNew = false);

    IUnitOfWork BeginTransaction();
}
