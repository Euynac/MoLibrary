using Microsoft.EntityFrameworkCore;
using Monica.JobScheduler.EfCore.Entities;
using Monica.JobScheduler.Exceptions;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Definitions;
using Monica.JobScheduler.Models.Execution;

namespace Monica.JobScheduler.EfCore;

/// <summary>
/// Implements the owner-snapshot definition half of the relational scheduler store.
/// </summary>
public sealed partial class EfCoreJobSchedulerStore
{
    /// <inheritdoc />
    public Task<JobDefinitionSyncResult> SyncOwnerSnapshotAsync(
        JobOwnerSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var normalized = snapshot.NormalizeAndValidate();
        return WriteAsync(async (dbContext, token) =>
        {
            var now = await GetUtcNowAsync(dbContext, token);
            var existingEntities = await dbContext.Definitions
                .Where(item => item.SchedulerScopeKey == normalized.SchedulerScopeKey
                               && item.OwnerKey == normalized.OwnerKey)
                .ToListAsync(token);
            var existingByJobKey = existingEntities.ToDictionary(
                static item => item.JobKey,
                StringComparer.Ordinal);
            var existingCursors = (await dbContext.RecurringCursors
                .Where(item => item.SchedulerScopeKey == normalized.SchedulerScopeKey
                               && item.OwnerKey == normalized.OwnerKey)
                .ToListAsync(token))
                .ToDictionary(static item => item.JobKey, StringComparer.Ordinal);

            var markedAbsent = 0;
            var presentKeys = normalized.Declarations
                .Select(static declaration => declaration.JobKey)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var entity in existingEntities.Where(item => !presentKeys.Contains(item.JobKey)))
            {
                entity.IsPresent = false;
                entity.LastObservedAtUtcTicks = ToTicks(now);
                entity.ConcurrencyToken = NewVersion();
                if (existingCursors.Remove(entity.JobKey, out var cursor))
                {
                    dbContext.RecurringCursors.Remove(cursor);
                }
                markedAbsent++;
            }

            foreach (var declaration in normalized.Declarations)
            {
                JobDefinitionEntity entity;
                if (existingByJobKey.TryGetValue(declaration.JobKey, out var existing))
                {
                    entity = existing;
                }
                else
                {
                    entity = new JobDefinitionEntity
                    {
                        SchedulerScopeKey = normalized.SchedulerScopeKey,
                        OwnerKey = normalized.OwnerKey,
                        JobKey = declaration.JobKey,
                        PolicyOverridesJson = Serialize(new JobPolicyOverrides()),
                        PolicyConcurrencyStamp = NewToken(),
                        PolicyUpdatedAtUtcTicks = ToTicks(now),
                        ConcurrencyToken = NewVersion()
                    };
                    dbContext.Definitions.Add(entity);
                }

                // Declaration fields follow the code; operator policy is sticky and never overwritten by a sync.
                entity.DeclarationJson = Serialize(declaration);
                entity.IsPresent = true;
                entity.LastObservedAtUtcTicks = ToTicks(now);
                if (declaration.JobType != JobType.Recurring
                    && existingCursors.Remove(declaration.JobKey, out var cursor))
                {
                    dbContext.RecurringCursors.Remove(cursor);
                }
                var definition = ToDefinition(entity, declaration);
                ApplyProjection(entity, definition);
                await ApplyEffectiveConcurrencyAsync(dbContext, definition, token);
            }

            return new JobDefinitionSyncResult
            {
                PresentCount = normalized.Declarations.Count,
                MarkedAbsentCount = markedAbsent,
                ObservedAtUtc = now
            };
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JobDefinition?> GetDefinitionAsync(
        string schedulerScopeKey,
        string ownerKey,
        string jobKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        ValidateIdentity(ownerKey, nameof(ownerKey));
        JobSchedulerIdentity.ValidateJobKey(jobKey, nameof(jobKey));
        return ReadAsync(async (dbContext, token) =>
        {
            var entity = await dbContext.Definitions.AsNoTracking().SingleOrDefaultAsync(
                item => item.SchedulerScopeKey == schedulerScopeKey
                        && item.OwnerKey == ownerKey
                        && item.JobKey == jobKey,
                token);
            return entity is null ? null : ToDefinition(entity);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<QueryResult<JobDefinition>> QueryDefinitionsAsync(
        string schedulerScopeKey,
        JobDefinitionQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        ArgumentNullException.ThrowIfNull(query);
        if (query.PageNumber < 1 || query.PageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Page number and size must be positive.");
        }
        return ReadAsync(async (dbContext, token) =>
        {
            var filtered = FilterDefinitions(dbContext, schedulerScopeKey, query);
            var totalCount = await filtered.CountAsync(token);
            var ordered = OrderDefinitions(filtered, query);
            var entities = await ordered
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(token);
            return new QueryResult<JobDefinition>(entities.Select(ToDefinition).ToList(), totalCount);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<JobPolicy> UpdatePolicyAsync(
        string schedulerScopeKey,
        string ownerKey,
        string jobKey,
        JobPolicyChange change,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(schedulerScopeKey, nameof(schedulerScopeKey));
        ValidateIdentity(ownerKey, nameof(ownerKey));
        JobSchedulerIdentity.ValidateJobKey(jobKey, nameof(jobKey));
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(change.Overrides);
        return WriteAsync(async (dbContext, token) =>
        {
            var now = await GetUtcNowAsync(dbContext, token);
            var entity = await dbContext.Definitions.SingleOrDefaultAsync(
                item => item.SchedulerScopeKey == schedulerScopeKey
                        && item.OwnerKey == ownerKey
                        && item.JobKey == jobKey,
                token)
                ?? throw new JobDefinitionNotFoundException(
                    $"Job '{ownerKey}/{jobKey}' was not found in scope '{schedulerScopeKey}'.");
            var current = ToPolicy(entity);
            var normalizedOverrides = change.Overrides.Normalize();
            if (!string.Equals(current.ConcurrencyStamp, change.ExpectedConcurrencyStamp, StringComparison.Ordinal))
            {
                throw new JobPolicyConcurrencyException(jobKey);
            }

            var declaration = Deserialize<JobDeclaration>(entity.DeclarationJson);
            normalizedOverrides.Validate(declaration, current.Overrides);

            var replacement = new JobPolicy
            {
                Overrides = normalizedOverrides,
                ConcurrencyStamp = NewToken(),
                UpdatedAtUtc = now
            };
            entity.PolicyOverridesJson = Serialize(normalizedOverrides);
            entity.PolicyConcurrencyStamp = replacement.ConcurrencyStamp;
            entity.PolicyUpdatedAtUtcTicks = ToTicks(now);
            var definition = ToDefinition(entity, declaration) with { Policy = replacement };
            ApplyProjection(entity, definition);
            await ApplyEffectiveConcurrencyAsync(dbContext, definition, token);
            await ApplyPolicyToRecurringCursorAsync(dbContext, definition, now, token);
            return replacement;
        }, cancellationToken);
    }

    private static IQueryable<JobDefinitionEntity> FilterDefinitions(
        JobSchedulerDbContext dbContext,
        string schedulerScopeKey,
        JobDefinitionQuery query)
    {
        var filtered = dbContext.Definitions.AsNoTracking()
            .Where(item => item.SchedulerScopeKey == schedulerScopeKey);
        if (!string.IsNullOrWhiteSpace(query.JobKey))
        {
            filtered = filtered.Where(item => item.JobKey == query.JobKey);
        }
        if (!string.IsNullOrWhiteSpace(query.OwnerKey))
        {
            filtered = filtered.Where(item => item.OwnerKey == query.OwnerKey);
        }
        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var search = query.SearchText.ToLower();
            filtered = filtered.Where(item =>
                item.JobKey.ToLower().Contains(search)
                || item.JobName.ToLower().Contains(search)
                || (item.Description != null && item.Description.ToLower().Contains(search)));
        }
        if (query.JobType is { } jobType)
        {
            filtered = filtered.Where(item => item.JobType == jobType);
        }
        if (query.IsDisabled is { } isDisabled)
        {
            filtered = filtered.Where(item => item.IsDisabled == isDisabled);
        }
        if (query.IsPresent is { } isPresent)
        {
            filtered = filtered.Where(item => item.IsPresent == isPresent);
        }

        return filtered;
    }

    private static IOrderedQueryable<JobDefinitionEntity> OrderDefinitions(
        IQueryable<JobDefinitionEntity> filtered,
        JobDefinitionQuery query) =>
        query.SortField switch
        {
            JobDefinitionSortField.JobName => Order(filtered, query, item => item.JobName)
                .ThenBy(item => item.JobKey),
            JobDefinitionSortField.JobKey => Order(filtered, query, item => item.JobKey),
            JobDefinitionSortField.OwnerKey => Order(filtered, query, item => item.OwnerKey)
                .ThenBy(item => item.JobKey),
            JobDefinitionSortField.JobType => Order(filtered, query, item => item.JobType)
                .ThenBy(item => item.JobKey),
            JobDefinitionSortField.IsDisabled => Order(filtered, query, item => item.IsDisabled)
                .ThenBy(item => item.JobKey),
            _ => throw new ArgumentOutOfRangeException(nameof(query), query.SortField, "Unsupported sort field.")
        };

    private static IOrderedQueryable<JobDefinitionEntity> Order<TKey>(
        IQueryable<JobDefinitionEntity> filtered,
        JobDefinitionQuery query,
        System.Linq.Expressions.Expression<Func<JobDefinitionEntity, TKey>> selector) =>
        query.SortDescending
            ? filtered.OrderByDescending(selector)
            : filtered.OrderBy(selector);

    private async Task ApplyEffectiveConcurrencyAsync(
        JobSchedulerDbContext dbContext,
        JobDefinition definition,
        CancellationToken cancellationToken)
    {
        var gate = await dbContext.ExecutionGates.SingleOrDefaultAsync(
            item => item.SchedulerScopeKey == definition.SchedulerScopeKey
                    && item.OwnerKey == definition.OwnerKey
                    && item.JobKey == definition.Declaration.JobKey,
            cancellationToken);
        if (gate is null)
        {
            gate = new JobExecutionGateEntity
            {
                SchedulerScopeKey = definition.SchedulerScopeKey,
                OwnerKey = definition.OwnerKey,
                JobKey = definition.Declaration.JobKey,
                ConcurrencyToken = NewVersion()
            };
            dbContext.ExecutionGates.Add(gate);
        }
        else
        {
            gate.ConcurrencyToken = NewVersion();
        }

        gate.MaxConcurrency = definition.EffectiveConfiguration.MaxConcurrency;
    }

    private static JobDefinition ToDefinition(JobDefinitionEntity entity)
    {
        return ToDefinition(entity, Deserialize<JobDeclaration>(entity.DeclarationJson));
    }

    private static JobDefinition ToDefinition(JobDefinitionEntity entity, JobDeclaration declaration) => new()
    {
        SchedulerScopeKey = entity.SchedulerScopeKey,
        OwnerKey = entity.OwnerKey,
        Declaration = declaration,
        Policy = ToPolicy(entity),
        IsPresent = entity.IsPresent,
        LastObservedAtUtc = FromTicks(entity.LastObservedAtUtcTicks)
    };

    private static JobPolicy ToPolicy(JobDefinitionEntity entity) => new()
    {
        Overrides = Deserialize<JobPolicyOverrides>(entity.PolicyOverridesJson),
        ConcurrencyStamp = entity.PolicyConcurrencyStamp,
        UpdatedAtUtc = FromTicks(entity.PolicyUpdatedAtUtcTicks)
    };

    private static void ApplyProjection(JobDefinitionEntity entity, JobDefinition definition)
    {
        var effective = definition.EffectiveConfiguration;
        entity.JobName = effective.JobName;
        entity.Description = effective.Description;
        entity.JobType = definition.Declaration.JobType;
        entity.IsDisabled = effective.IsDisabled;
    }
}
