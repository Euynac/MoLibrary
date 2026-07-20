namespace Monica.Testing.Repository;

/// <summary>
/// Describes how a <see cref="DbContextFixture{TDbContext}"/> configures its EF Core provider.
/// </summary>
public enum DbContextFixtureMode
{
    /// <summary>
    /// Uses EF Core's in-memory provider with an isolated database name.
    /// </summary>
    EfInMemory,

    /// <summary>
    /// Uses a SQLite in-memory database backed by an open connection held for the fixture lifetime.
    /// </summary>
    SqliteInMemory,

    /// <summary>
    /// Uses caller-supplied DbContext options, typically for provider-specific integration tests.
    /// </summary>
    CustomOptions
}
