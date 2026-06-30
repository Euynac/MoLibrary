using Microsoft.EntityFrameworkCore;
using Monica.Modules;

namespace Monica.Repository.Persistence.Models;

/// <summary>
/// Describes a Repository DbContext registration captured when the host calls
/// <c>AddRepositoryDbContext&lt;TDbContext&gt;</c>.
/// </summary>
public sealed record RepositoryDbContextRegistration
{
    /// <summary>
    /// Creates a new registration descriptor.
    /// </summary>
    /// <param name="dbContextType">The registered DbContext type.</param>
    /// <param name="providerType">How repositories resolve the DbContext in scoped operations.</param>
    /// <param name="registrationOrder">The order in which the context was registered.</param>
    public RepositoryDbContextRegistration(
        Type dbContextType,
        DbContextProviderType providerType,
        int registrationOrder)
    {
        DbContextType = dbContextType;
        ProviderType = providerType;
        RegistrationOrder = registrationOrder;
    }

    /// <summary>
    /// Stable identifier used by diagnostics callers to select this DbContext.
    /// </summary>
    public string ContextId => DbContextType.AssemblyQualifiedName ?? DbContextType.FullName ?? DbContextType.Name;

    /// <summary>
    /// Registered DbContext type.
    /// </summary>
    public Type DbContextType { get; }

    /// <summary>
    /// Human-readable DbContext name.
    /// </summary>
    public string DbContextName => DbContextType.Name;

    /// <summary>
    /// Fully qualified DbContext type name.
    /// </summary>
    public string FullName => DbContextType.FullName ?? DbContextType.Name;

    /// <summary>
    /// Assembly containing the DbContext type.
    /// </summary>
    public string AssemblyName => DbContextType.Assembly.GetName().Name ?? string.Empty;

    /// <summary>
    /// How repositories resolve the DbContext in scoped operations.
    /// </summary>
    public DbContextProviderType ProviderType { get; }

    /// <summary>
    /// Registration order captured from module configuration.
    /// </summary>
    public int RegistrationOrder { get; }
}

/// <summary>
/// Runtime snapshot for a registered Repository DbContext.
/// </summary>
public sealed record RepositoryDbContextSnapshot
{
    /// <summary>
    /// Static registration metadata.
    /// </summary>
    public required RepositoryDbContextRegistration Registration { get; init; }

    /// <summary>
    /// EF Core provider name reported by the DbContext.
    /// </summary>
    public string? ProviderName { get; init; }

    /// <summary>
    /// Migration assembly name when EF Core exposes one.
    /// </summary>
    public string? MigrationAssembly { get; init; }

    /// <summary>
    /// Database connection details prepared for diagnostics display.
    /// </summary>
    public RepositoryConnectionInfo? Connection { get; init; }

    /// <summary>
    /// Whether the database accepted a connection probe.
    /// </summary>
    public bool CanConnect { get; init; }

    /// <summary>
    /// Number of pending migrations when migration metadata can be read.
    /// </summary>
    public int? PendingMigrationCount { get; init; }

    /// <summary>
    /// Number of known migrations when migration metadata can be read.
    /// </summary>
    public int? MigrationCount { get; init; }

    /// <summary>
    /// Flattened Repository and EF option entries suitable for property-grid display.
    /// </summary>
    public IReadOnlyList<RepositoryOptionEntry> Options { get; init; } = [];

    /// <summary>
    /// Error message captured while building this snapshot.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Whether snapshot collection hit an error for this context.
    /// </summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
}

/// <summary>
/// Safe connection-string and connection endpoint details for a Repository DbContext.
/// </summary>
public sealed record RepositoryConnectionInfo
{
    /// <summary>
    /// Provider-specific data source, server, or host value.
    /// </summary>
    public string? DataSource { get; init; }

    /// <summary>
    /// Database name reported by the provider.
    /// </summary>
    public string? Database { get; init; }

    /// <summary>
    /// Masked connection string with sensitive values replaced.
    /// </summary>
    public string? MaskedConnectionString { get; init; }

    /// <summary>
    /// Full connection string. This is populated only when a caller explicitly requests reveal.
    /// </summary>
    public string? RevealedConnectionString { get; init; }

    /// <summary>
    /// Whether the full connection string is present.
    /// </summary>
    public bool IsRevealed => !string.IsNullOrEmpty(RevealedConnectionString);
}

/// <summary>
/// Display-ready option entry for Repository diagnostics.
/// </summary>
public sealed record RepositoryOptionEntry
{
    /// <summary>
    /// Option path, such as <c>ModuleRepository.EnableEfCoreConnectionMetrics</c>.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Human-readable option label.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Formatted option value.
    /// </summary>
    public required string Value { get; init; }
}

/// <summary>
/// Result of reading database activity for a selected Repository DbContext.
/// </summary>
public sealed record RepositoryActivityResult
{
    /// <summary>
    /// Selected context registration metadata.
    /// </summary>
    public required RepositoryDbContextRegistration Registration { get; init; }

    /// <summary>
    /// Activity support status for the selected provider.
    /// </summary>
    public RepositoryActivitySupport Support { get; init; }

    /// <summary>
    /// Provider name reported by EF Core.
    /// </summary>
    public string? ProviderName { get; init; }

    /// <summary>
    /// Activity rows ordered by runtime descending.
    /// </summary>
    public IReadOnlyList<RepositoryActivityRow> Rows { get; init; } = [];

    /// <summary>
    /// Error or unsupported-provider message for display.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Number of returned rows.
    /// </summary>
    public int SessionCount => Rows.Count;
}

/// <summary>
/// Activity support status for a Repository DbContext provider.
/// </summary>
public enum RepositoryActivitySupport
{
    /// <summary>
    /// Activity query is supported and completed.
    /// </summary>
    Supported,

    /// <summary>
    /// The EF provider is not supported by the activity query catalog.
    /// </summary>
    UnsupportedProvider,

    /// <summary>
    /// The activity query failed.
    /// </summary>
    Failed
}

/// <summary>
/// Normalized database activity row used by the Repository UI.
/// </summary>
public sealed record RepositoryActivityRow
{
    /// <summary>
    /// Query runtime.
    /// </summary>
    public TimeSpan? Runtime { get; init; }

    /// <summary>
    /// Database name.
    /// </summary>
    public string? DatabaseName { get; init; }

    /// <summary>
    /// Backend process or session identifier.
    /// </summary>
    public string? BackendId { get; init; }

    /// <summary>
    /// User name associated with the session.
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// Application name associated with the session.
    /// </summary>
    public string? ApplicationName { get; init; }

    /// <summary>
    /// Client address.
    /// </summary>
    public string? ClientAddress { get; init; }

    /// <summary>
    /// Client host name.
    /// </summary>
    public string? ClientHostname { get; init; }

    /// <summary>
    /// Client port.
    /// </summary>
    public int? ClientPort { get; init; }

    /// <summary>
    /// Backend start time.
    /// </summary>
    public DateTime? BackendStart { get; init; }

    /// <summary>
    /// Transaction start time.
    /// </summary>
    public DateTime? TransactionStart { get; init; }

    /// <summary>
    /// Query start time.
    /// </summary>
    public DateTime? QueryStart { get; init; }

    /// <summary>
    /// Last state change time.
    /// </summary>
    public DateTime? StateChange { get; init; }

    /// <summary>
    /// Whether the session is waiting.
    /// </summary>
    public bool? Waiting { get; init; }

    /// <summary>
    /// Provider-specific state value.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// Current or last query text.
    /// </summary>
    public string? Query { get; init; }

    /// <summary>
    /// Optional provider-specific detail value.
    /// </summary>
    public string? Detail { get; init; }
}

/// <summary>
/// Migration inventory for a selected Repository DbContext.
/// </summary>
public sealed record RepositoryMigrationStatus
{
    /// <summary>
    /// Selected context registration metadata.
    /// </summary>
    public required RepositoryDbContextRegistration Registration { get; init; }

    /// <summary>
    /// EF Core provider name.
    /// </summary>
    public string? ProviderName { get; init; }

    /// <summary>
    /// Migration assembly name.
    /// </summary>
    public string? MigrationAssembly { get; init; }

    /// <summary>
    /// All migrations known to the DbContext.
    /// </summary>
    public IReadOnlyList<string> AllMigrations { get; init; } = [];

    /// <summary>
    /// Migrations already applied to the database.
    /// </summary>
    public IReadOnlyList<string> AppliedMigrations { get; init; } = [];

    /// <summary>
    /// Migrations still pending.
    /// </summary>
    public IReadOnlyList<string> PendingMigrations { get; init; } = [];

    /// <summary>
    /// Migrations from the selected DbContext migration assembly with their current database state.
    /// </summary>
    public IReadOnlyList<RepositoryMigrationAssemblyItem> CurrentAssemblyMigrations
    {
        get
        {
            var appliedMigrations = AppliedMigrations.ToHashSet(StringComparer.Ordinal);
            var pendingMigrations = PendingMigrations.ToHashSet(StringComparer.Ordinal);

            return AllMigrations
                .Select(migrationId => new RepositoryMigrationAssemblyItem
                {
                    MigrationId = migrationId,
                    State = ResolveCurrentAssemblyMigrationState(migrationId, appliedMigrations, pendingMigrations)
                })
                .ToList();
        }
    }

    /// <summary>
    /// Applied history-table rows that belong to this DbContext's current migration assembly.
    /// </summary>
    public IReadOnlyList<string> AppliedCurrentAssemblyMigrations
    {
        get
        {
            var appliedMigrations = AppliedMigrations.ToHashSet(StringComparer.Ordinal);
            return AllMigrations
                .Where(appliedMigrations.Contains)
                .ToList();
        }
    }

    /// <summary>
    /// Applied history-table rows that are not part of this DbContext's current migration assembly.
    /// </summary>
    public IReadOnlyList<string> ExternalAppliedMigrations
    {
        get
        {
            var currentAssemblyMigrations = AllMigrations.ToHashSet(StringComparer.Ordinal);
            return AppliedMigrations
                .Where(migration => !currentAssemblyMigrations.Contains(migration))
                .ToList();
        }
    }

    /// <summary>
    /// Error message captured while loading migration status.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Whether the migration status could be read.
    /// </summary>
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>
    /// Whether this DbContext has migrations waiting to be applied.
    /// </summary>
    public bool HasPendingMigrations => PendingMigrations.Count > 0;

    /// <summary>
    /// Whether the database migration history contains rows outside this DbContext's current migration assembly.
    /// </summary>
    public bool HasExternalAppliedMigrations => ExternalAppliedMigrations.Count > 0;

    private static RepositoryMigrationApplicationState ResolveCurrentAssemblyMigrationState(
        string migrationId,
        IReadOnlySet<string> appliedMigrations,
        IReadOnlySet<string> pendingMigrations)
    {
        if (pendingMigrations.Contains(migrationId))
        {
            return RepositoryMigrationApplicationState.Pending;
        }

        return appliedMigrations.Contains(migrationId)
            ? RepositoryMigrationApplicationState.Applied
            : RepositoryMigrationApplicationState.Unknown;
    }
}

/// <summary>
/// Display-ready migration entry from the selected DbContext's current migration assembly.
/// </summary>
public sealed record RepositoryMigrationAssemblyItem
{
    /// <summary>
    /// EF Core migration identifier.
    /// </summary>
    public required string MigrationId { get; init; }

    /// <summary>
    /// Database application state for this migration.
    /// </summary>
    public RepositoryMigrationApplicationState State { get; init; }
}

/// <summary>
/// Application state for a migration known by the selected DbContext's current migration assembly.
/// </summary>
public enum RepositoryMigrationApplicationState
{
    /// <summary>
    /// The migration is recorded in the database history table.
    /// </summary>
    Applied,

    /// <summary>
    /// The migration is known by the current assembly but is not yet applied.
    /// </summary>
    Pending,

    /// <summary>
    /// The migration was neither reported as pending nor found in the database history table.
    /// </summary>
    Unknown
}

/// <summary>
/// Result of applying pending migrations for one Repository DbContext.
/// </summary>
public sealed record RepositoryMigrationUpdateResult
{
    /// <summary>
    /// Selected context registration metadata.
    /// </summary>
    public required RepositoryDbContextRegistration Registration { get; init; }

    /// <summary>
    /// Pending migrations detected before the update ran.
    /// </summary>
    public IReadOnlyList<string> AttemptedMigrations { get; init; } = [];

    /// <summary>
    /// Whether the update operation completed successfully.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Whether there were no pending migrations to apply.
    /// </summary>
    public bool Skipped { get; init; }

    /// <summary>
    /// Operation duration.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// User-facing result message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Error message when the update failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
