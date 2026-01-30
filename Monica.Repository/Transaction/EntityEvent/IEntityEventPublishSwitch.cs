namespace Monica.Repository.Transaction.EntityEvent;

/// <summary>
/// Provides a hook to decide whether an entity change event should be published.
/// </summary>
public interface IEntityEventPublishSwitch
{
    /// <summary>
    /// Determines whether the current entity change event is allowed to be published.
    /// </summary>
    /// <param name="entity">The entity whose change triggered the event.</param>
    /// <returns><c>true</c> if publishing is allowed; otherwise, <c>false</c>.</returns>
    bool CanPublish(object entity);

    /// <summary>
    /// Gets whether automatic synchronization is currently enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Suspends automatic synchronization for the current asynchronous flow.
    /// </summary>
    /// <returns>An <see cref="IDisposable"/> that resumes synchronization when disposed.</returns>
    IDisposable Suspend();
}