using Microsoft.EntityFrameworkCore;
using Monica.DependencyInjection.Abstractions;
using Monica.Repository.UnitOfWork.Models;
using Monica.Repository.UnitOfWork.Services;

namespace Monica.Repository.UnitOfWork.Abstractions;

/// <summary>
/// Framework-only operations required to bind DbContexts and ambient state to a unit of work.
/// </summary>
/// <remarks>
/// Consumers should depend on <see cref="IUnitOfWork"/>. This interface exists for infrastructure components such as
/// the unit-of-work manager, DbContext provider, and event buffer store.
/// </remarks>
internal interface IUnitOfWorkInternals
{
    /// <summary>
    /// Gets the options used to create the unit of work.
    /// </summary>
    UnitOfWorkScopeOptions Options { get; }

    /// <summary>
    /// Gets whether the unit of work has been disposed.
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    /// Gets the ambient unit of work that was active before this one was created.
    /// </summary>
    IUnitOfWork? Outer { get; }

    /// <summary>
    /// Gets the scoped service provider owned by this unit of work.
    /// </summary>
    ICachedServiceProvider CachedServiceProvider { get; }

    /// <summary>
    /// Sets the outer ambient unit of work.
    /// </summary>
    void SetOuter(IUnitOfWork? outer);

    /// <summary>
    /// Attaches a DbContext instance to this unit of work.
    /// </summary>
    void AttachDbContext<TDbContext>(TDbContext dbContext)
        where TDbContext : DbContext;

    /// <summary>
    /// Gets an attached DbContext instance when one already exists.
    /// </summary>
    TDbContext? TryGetDbContext<TDbContext>()
        where TDbContext : DbContext;

    /// <summary>
    /// Registers a synchronous handler that runs when the unit of work is disposed.
    /// </summary>
    void OnDisposed(Action handler);

    /// <summary>
    /// Gets the current transaction event buffer, if one has been created.
    /// </summary>
    AsyncEventBuffer? GetEventBuffer();

    /// <summary>
    /// Gets the current transaction event buffer or creates one when needed.
    /// </summary>
    AsyncEventBuffer GetOrCreateEventBuffer();
}
