using Monica.JobScheduler.Abstractions;

namespace Monica.Testing.ProjectUnits;

/// <summary>
/// Convenience execution helpers for job ProjectUnit fixtures.
/// </summary>
public static class JobFixtureExtensions
{
    /// <summary>
    /// Executes the recurring job owned by the fixture.
    /// </summary>
    public static Task ExecuteAsync<TJob>(
        this ProjectUnitFixture<TJob> fixture,
        CancellationToken cancellationToken = default)
        where TJob : RecurringJob
    {
        ArgumentNullException.ThrowIfNull(fixture);
        return fixture.Unit.ExecuteAsync(cancellationToken);
    }

    /// <summary>
    /// Executes the triggered job owned by the fixture with typed parameters.
    /// </summary>
    public static Task ExecuteAsync<TJob, TArgs>(
        this ProjectUnitFixture<TJob> fixture,
        TArgs parameters,
        CancellationToken cancellationToken = default)
        where TJob : TriggeredJob<TArgs>
        where TArgs : class
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(parameters);
        return fixture.Unit.ExecuteAsync(parameters, cancellationToken);
    }
}
