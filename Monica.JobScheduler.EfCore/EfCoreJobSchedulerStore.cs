using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.EfCore.Entities;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Definitions;
using Monica.JobScheduler.Models.Execution;
using Monica.Modules;

namespace Monica.JobScheduler.EfCore;

/// <summary>
/// Persists the immutable job catalog and durable execution queue in one relational database.
/// </summary>
/// <remarks>
/// Every compound mutation uses a serializable transaction and optimistic row fencing. The store is safe to inject
/// into singleton hosted services because every operation creates and disposes its own DbContext. Claims use the
/// same provider-neutral serializable boundary; PostgreSQL may abort competing claim transactions and the store
/// retries them instead of relying on process-local coordination.
/// </remarks>
public sealed partial class EfCoreJobSchedulerStore(
    IDbContextFactory<JobSchedulerDbContext> dbContextFactory,
    TimeProvider? timeProvider = null,
    IOptions<ModuleJobSchedulerOption>? options = null) : IJobSchedulerStore
{
    private const int MAX_TRANSACTION_ATTEMPTS = 5;
    private static readonly JsonSerializerOptions JSON_OPTIONS = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<JobSchedulerDbContext> _dbContextFactory =
        dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly JobExecutionHistoryLimits _historyLimits = new(
        options?.Value.MaxExecutionHistoryEntriesPerExecution
        ?? JobExecutionHistoryLimits.DEFAULT_MAX_ENTRIES,
        options?.Value.MaxExecutionHistoryMessageLength
        ?? JobExecutionHistoryLimits.DEFAULT_MAX_MESSAGE_LENGTH);

    private DateTimeOffset UtcNow => NormalizeUtc(_timeProvider.GetUtcNow());

    private async Task<DateTimeOffset> GetUtcNowAsync(
        JobSchedulerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var providerName = dbContext.Database.ProviderName;
        if (providerName is not null
            && (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
                || providerName.Contains("Postgre", StringComparison.OrdinalIgnoreCase)
                || providerName.Contains("Gauss", StringComparison.OrdinalIgnoreCase)))
        {
            var databaseTime = await dbContext.Database
                // CURRENT_TIMESTAMP is fixed at transaction start on PostgreSQL-family providers. Policy schedule
                // replacement needs wall-clock database time after any lock wait so its prospective cursor cannot
                // admit an occurrence that became historical while the transaction was waiting.
                .SqlQuery<DateTime>($"SELECT (clock_timestamp() AT TIME ZONE 'UTC') AS \"Value\"")
                .SingleAsync(cancellationToken);
            return new DateTimeOffset(DateTime.SpecifyKind(databaseTime, DateTimeKind.Utc));
        }

        return UtcNow;
    }

    private async Task<TResult> ReadAsync<TResult>(
        Func<JobSchedulerDbContext, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await operation(dbContext, cancellationToken);
    }

    private async Task<TResult> WriteAsync<TResult>(
        Func<JobSchedulerDbContext, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = dbContext.Database.IsRelational()
                ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            try
            {
                var result = await operation(dbContext, cancellationToken);
                await TrimExecutionHistoryAsync(dbContext, cancellationToken);
                SetMutationVersions(dbContext);
                await dbContext.SaveChangesOnDbContextAsync(true, cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return result;
            }
            catch (Exception exception) when (attempt < MAX_TRANSACTION_ATTEMPTS && IsRetryable(exception))
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(attempt * 10), cancellationToken);
            }
        }
    }

    private static bool IsRetryable(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException or DbUpdateException)
        {
            return true;
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            var type = current.GetType();
            var sqlState = type.GetProperty("SqlState")?.GetValue(current) as string;
            if (sqlState is "40001" or "40P01" or "23505")
            {
                return true;
            }

            if (type.GetProperty("SqliteErrorCode")?.GetValue(current) is int errorCode
                && errorCode is 5 or 6)
            {
                return true;
            }
        }

        return false;
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JSON_OPTIONS);

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JSON_OPTIONS)
        ?? throw new InvalidOperationException($"Stored scheduler payload '{typeof(T).Name}' was empty.");

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value) => value.ToUniversalTime();
    private static long ToTicks(DateTimeOffset value) => NormalizeUtc(value).UtcDateTime.Ticks;
    private static long? ToTicks(DateTimeOffset? value) => value is null ? null : ToTicks(value.Value);
    private static DateTimeOffset FromTicks(long value) => new(value, TimeSpan.Zero);
    private static DateTimeOffset? FromTicks(long? value) => value is null ? null : FromTicks(value.Value);
    private static string NewToken() => Guid.NewGuid().ToString("N");
    private static Guid NewVersion() => Guid.NewGuid();

    private static void SetMutationVersions(JobSchedulerDbContext dbContext)
    {
        foreach (var entry in dbContext.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            var property = entry.Metadata.FindProperty("ConcurrencyToken");
            if (property?.ClrType == typeof(Guid))
            {
                entry.Property("ConcurrencyToken").CurrentValue = NewVersion();
            }
        }
    }

    private async Task TrimExecutionHistoryAsync(
        JobSchedulerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var pendingEntries = dbContext.ChangeTracker
            .Entries<JobExecutionHistoryEntity>()
            .Where(static entry => entry.State == EntityState.Added)
            .Select(static entry => entry.Entity)
            .ToArray();
        // Sequence numbers never repeat. Once a new sequence is appended, retaining only the final MaxEntries-wide
        // sequence window gives deterministic oldest-first eviction without counting or loading the history stream.
        var boundaries = pendingEntries
            .GroupBy(static entry => (entry.SchedulerScopeKey, entry.InstanceId))
            .Select(group => new
            {
                group.Key.SchedulerScopeKey,
                group.Key.InstanceId,
                EvictionSequenceBoundary = group.Max(static entry => entry.Sequence)
                                           - _historyLimits.MaxEntries
            })
            .Where(static boundary => boundary.EvictionSequenceBoundary >= 1)
            .ToArray();

        foreach (var boundary in boundaries)
        {
            await dbContext.ExecutionHistory
                .Where(entry => entry.SchedulerScopeKey == boundary.SchedulerScopeKey
                                && entry.InstanceId == boundary.InstanceId
                                && entry.Sequence <= boundary.EvictionSequenceBoundary)
                .ExecuteDeleteAsync(cancellationToken);

            foreach (var pendingEntry in pendingEntries.Where(entry =>
                         entry.SchedulerScopeKey == boundary.SchedulerScopeKey
                         && entry.InstanceId == boundary.InstanceId
                         && entry.Sequence <= boundary.EvictionSequenceBoundary))
            {
                dbContext.Entry(pendingEntry).State = EntityState.Detached;
            }
        }
    }

    private static void ValidateIdentity(string value, string parameterName) =>
        JobSchedulerIdentity.ValidateStandard(value, parameterName);
}
