using System.Text.Json;
using Microsoft.Extensions.Options;
using Monica.JobScheduler.Abstractions;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Catalog;
using Monica.JobScheduler.Models.Execution;
using Monica.Modules;

namespace Monica.JobScheduler.Services;

/// <summary>
/// Admits local typed triggered jobs directly into the durable queue with an executable-revision fence.
/// </summary>
internal sealed class TriggeredJobManager(
    IReadOnlyList<LocalJobDefinition> localDefinitions,
    IJobSchedulerStore store,
    JobSchedulerHostIdentity identity,
    IOptions<ModuleJobSchedulerOption> options,
    TimeProvider timeProvider) : ITriggeredJobManager
{
    private readonly IReadOnlyDictionary<Type, LocalJobDefinition> _jobsByArguments = localDefinitions
        .Where(static definition => definition.JobArgsClrType is not null)
        .ToDictionary(static definition => definition.JobArgsClrType!);

    /// <inheritdoc />
    public async Task<string> EnqueueAsync<TArgs>(
        TArgs args,
        TimeSpan? delay = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (delay is { } value && value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), value, "Delay cannot be negative.");
        }

        if (!_jobsByArguments.TryGetValue(typeof(TArgs), out var localDefinition))
        {
            throw new InvalidOperationException(
                $"No local triggered job accepts arguments of type '{typeof(TArgs).FullName}'.");
        }

        var declaration = localDefinition.Declaration;
        var ownerId = identity.LocalOwnerId
                      ?? throw new InvalidOperationException("The current host has no worker owner identity.");
        var revision = identity.LocalWorkerRevisionId
                       ?? throw new InvalidOperationException("The current host has no worker revision identity.");
        var instanceId = Guid.NewGuid().ToString("N");
        await store.EnqueueAsync(new JobEnqueueRequest
        {
            InstanceId = instanceId,
            SchedulerScopeKey = identity.ReleaseStage.Manifest.SchedulerScopeKey,
            JobKey = declaration.JobKey,
            ExpectedOwnerId = ownerId,
            ExpectedJobRevisionId = JobCatalogHash.ComputeJobRevision(
                ownerId,
                revision,
                declaration),
            JobArgs = JsonSerializer.Serialize(args, options.Value.JobArgsSerializerOptions),
            AvailableAtUtc = timeProvider.GetUtcNow().Add(delay ?? TimeSpan.Zero),
            EnqueueReason = delay is null
                ? "Triggered execution enqueued"
                : $"Triggered execution delayed by {delay.Value}"
        }, cancellationToken);
        return instanceId;
    }

    /// <inheritdoc />
    public Task<JobCancellationResult> CancelExecutionAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        return store.RequestCancellationAsync(
            identity.ReleaseStage.Manifest.SchedulerScopeKey,
            instanceId,
            "Cancellation requested by application",
            cancellationToken);
    }
}
