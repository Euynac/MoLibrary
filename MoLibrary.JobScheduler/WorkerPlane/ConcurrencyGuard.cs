using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MoLibrary.JobScheduler.Abstractions;
using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.WorkerPlane;

/// <summary>
/// Enforces maximum concurrency limits per job key to prevent resource contention.
/// Tracks both in-memory counters and queries the metadata store for real-time processing counts
/// to handle worker crashes and distributed execution scenarios.
/// </summary>
/// <remarks>
/// <para>
/// ConcurrencyGuard provides thread-safe concurrency control for job execution.
/// It maintains an in-memory counter for performance while also querying the metadata store
/// for the authoritative processing count to handle edge cases like:
/// </para>
/// <list type="bullet">
/// <item><description>Worker crashes that don't release counters</description></item>
/// <item><description>Distributed workers executing the same job type</description></item>
/// <item><description>Counter drift due to failed state updates</description></item>
/// </list>
/// <para>
/// <b>Thread Safety:</b>
/// This class uses ConcurrentDictionary for thread-safe counter management.
/// Multiple threads can safely call TryAcquireAsync and ReleaseAsync concurrently.
/// </para>
/// </remarks>
public class ConcurrencyGuard(
    IMoJobScheduleMetadataStore metadataStore,
    ILogger<ConcurrencyGuard> logger)
{
    /// <summary>
    /// In-memory counter tracking current processing count per job key.
    /// Used for fast local checks and to track releases.
    /// Synchronized with metadata store during acquisition attempts.
    /// </summary>
    private readonly ConcurrentDictionary<string, int> _processingCounters = new();

    /// <summary>
    /// Attempts to acquire an execution slot for the specified job.
    /// Queries the metadata store for real-time processing count to ensure accurate enforcement
    /// across distributed workers and crash recovery scenarios.
    /// </summary>
    /// <param name="jobKey">The unique job key for which to acquire a slot.</param>
    /// <param name="maxConcurrency">The maximum allowed concurrent executions for this job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// True if a slot was successfully acquired, false if the concurrency limit is reached.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method performs the following steps:
    /// </para>
    /// <list type="number">
    /// <item><description>Queries the metadata store for the current count of jobs in Processing state</description></item>
    /// <item><description>Compares the count to maxConcurrency</description></item>
    /// <item><description>If below limit, atomically increments the in-memory counter</description></item>
    /// <item><description>Returns success/failure indication</description></item>
    /// </list>
    /// <para>
    /// The metadata store query provides the authoritative count, ensuring correct behavior
    /// even after worker crashes or in distributed scenarios. The in-memory counter is then
    /// used to track local acquisitions for efficient release operations.
    /// </para>
    /// </remarks>
    public async Task<bool> TryAcquireAsync(
        string jobKey,
        int maxConcurrency,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty.", nameof(jobKey));
        }

        if (maxConcurrency <= 0)
        {
            logger.LogWarning(
                "Invalid maxConcurrency value {MaxConcurrency} for job {JobKey}. Must be > 0.",
                maxConcurrency,
                jobKey);
            return false;
        }

        try
        {
            // Query metadata store for authoritative processing count
            // This ensures we have the real-time count across all workers
            var currentProcessingCount = await metadataStore.GetProcessingCountAsync(jobKey, cancellationToken);

            logger.LogDebug(
                "Concurrency check for job {JobKey}: current={Current}, max={Max}",
                jobKey,
                currentProcessingCount,
                maxConcurrency);

            // Check if we can acquire a slot
            if (currentProcessingCount >= maxConcurrency)
            {
                logger.LogInformation(
                    "Concurrency limit reached for job {JobKey}. Current: {Current}, Max: {Max}",
                    jobKey,
                    currentProcessingCount,
                    maxConcurrency);
                return false;
            }

            // Acquire slot by incrementing in-memory counter
            // This counter tracks local acquisitions for release tracking
            _processingCounters.AddOrUpdate(
                jobKey,
                1, // Add if not exists
                (_, existingCount) => existingCount + 1); // Increment if exists

            var newLocalCount = _processingCounters[jobKey];

            logger.LogInformation(
                "Concurrency slot acquired for job {JobKey}. Store count: {StoreCount}, Local count: {LocalCount}, Max: {Max}",
                jobKey,
                currentProcessingCount,
                newLocalCount,
                maxConcurrency);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error during concurrency acquisition for job {JobKey}: {Message}",
                jobKey,
                ex.Message);

            // On error, fail safe by denying acquisition to avoid over-subscription
            return false;
        }
    }

    /// <summary>
    /// Releases an execution slot for the specified job by decrementing the in-memory counter.
    /// This should be called after a job completes execution (success or failure).
    /// </summary>
    /// <param name="jobKey">The unique job key for which to release a slot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// <para>
    /// This method safely decrements the in-memory processing counter.
    /// If the counter reaches zero, it is removed from the dictionary to prevent memory leaks.
    /// </para>
    /// <para>
    /// <b>Edge Case Handling:</b>
    /// If the counter goes negative (which should never happen in normal operation but could
    /// occur due to bugs or double-release), a warning is logged and the counter is reset to zero.
    /// </para>
    /// </remarks>
    public Task ReleaseAsync(string jobKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
        {
            throw new ArgumentException("Job key cannot be null or empty.", nameof(jobKey));
        }

        try
        {
            // Decrement counter atomically
            var newCount = _processingCounters.AddOrUpdate(
                jobKey,
                0, // If key doesn't exist, initialize to 0 (edge case: release without acquire)
                (_, existingCount) =>
                {
                    // Decrement, but don't go below zero
                    var newValue = existingCount - 1;
                    return Math.Max(0, newValue);
                });

            // Log warning if counter was already at zero (potential double-release)
            if (_processingCounters.TryGetValue(jobKey, out var currentCount))
            {
                if (currentCount < 0)
                {
                    logger.LogWarning(
                        "Concurrency counter went negative for job {JobKey}. Resetting to 0. This indicates a potential bug.",
                        jobKey);

                    // Reset to 0 to prevent further issues
                    _processingCounters.TryUpdate(jobKey, 0, currentCount);
                }
                else if (newCount == 0)
                {
                    // Clean up entry when counter reaches zero to prevent dictionary bloat
                    _processingCounters.TryRemove(jobKey, out _);

                    logger.LogDebug(
                        "Concurrency counter for job {JobKey} reached zero and was removed",
                        jobKey);
                }
            }

            logger.LogDebug(
                "Concurrency slot released for job {JobKey}. Local count now: {LocalCount}",
                jobKey,
                newCount);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error during concurrency release for job {JobKey}: {Message}",
                jobKey,
                ex.Message);

            // Don't rethrow - release should always succeed even if logging fails
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the current in-memory processing count for a job (for diagnostics/testing).
    /// Note: This is the local counter only, not the authoritative metadata store count.
    /// </summary>
    /// <param name="jobKey">The job key to query.</param>
    /// <returns>The current in-memory processing count, or 0 if not tracked.</returns>
    public int GetLocalProcessingCount(string jobKey)
    {
        return _processingCounters.TryGetValue(jobKey, out var count) ? count : 0;
    }
}
