using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using Monica.JobScheduler.Exceptions;
using Monica.JobScheduler.Models;
using Monica.Tool.Extensions;

namespace Monica.JobScheduler.Services;

/// <summary>
/// Owns the immutable local job-type catalog used by this host's execution plane.
/// </summary>
/// <remarks>
/// The local catalog is built when this singleton is created on every host, including Worker hosts that never acquire
/// service-discovery leadership. Persistent definition reconciliation is independently owned by the elected control
/// plane and never mutates this process-local CLR-type catalog.
/// </remarks>
public class JobRegistry(
    IReadOnlyList<JobDefinition> localJobDefinitions,
    ILogger<JobRegistry> logger)
{
    private readonly LocalJobCatalog _localJobs = LocalJobCatalog.Create(localJobDefinitions, logger);

    /// <summary>
    /// Retrieves the CLR type for a job by its job key.
    /// </summary>
    /// <param name="jobKey">The unique job key (job type's full name).</param>
    /// <returns>The CLR type of the job, or null if not found.</returns>
    public Type? GetJobClrType(string jobKey)
    {
        return _localJobs.JobTypesByKey.GetValueOrDefault(jobKey);
    }
    
    /// <summary>
    /// Retrieves the CLR type for a triggered job by its job args key.
    /// </summary>
    /// <param name="jobArgsKey"></param>
    /// <returns></returns>
    public Type? GetTriggeredJobClrType(string jobArgsKey)
    {
        return _localJobs.TriggeredJobTypesByArgsKey.GetValueOrDefault(jobArgsKey);
    }

    /// <summary>
    /// Retrieves the CLR type for job arguments by its job args key.
    /// </summary>
    /// <param name="jobArgsKey">The unique job args key (args type's full name).</param>
    /// <returns>The CLR type of the job arguments, or null if not found.</returns>
    public Type? GetJobArgsClrType(string jobArgsKey)
    {
        return _localJobs.TriggeredArgsTypesByKey.GetValueOrDefault(jobArgsKey);
    }

    private sealed record LocalJobCatalog(
        FrozenDictionary<string, Type> JobTypesByKey,
        FrozenDictionary<string, Type> TriggeredJobTypesByArgsKey,
        FrozenDictionary<string, Type> TriggeredArgsTypesByKey)
    {
        internal static LocalJobCatalog Create(
            IReadOnlyList<JobDefinition> definitions,
            ILogger<JobRegistry> logger)
        {
            ArgumentNullException.ThrowIfNull(definitions);

            var jobTypesByKey = new Dictionary<string, Type>(StringComparer.Ordinal);
            var triggeredJobTypesByArgsKey = new Dictionary<string, Type>(StringComparer.Ordinal);
            var triggeredArgsTypesByKey = new Dictionary<string, Type>(StringComparer.Ordinal);

            foreach (var definition in definitions)
            {
                if (!jobTypesByKey.TryAdd(definition.JobKey, definition.JobClrType))
                {
                    throw new JobRegistrationException(
                        $"Duplicate local job key '{definition.JobKey}'.",
                        definition.JobKey);
                }

                logger.LogInformation(
                    "Registered local job: {JobKey} | Name: {JobName} | Type: {JobType} | MaxConcurrency: {MaxConcurrency} | RetryCount: {RetryCount} | Timeout: {Timeout}",
                    definition.JobKey,
                    definition.JobName,
                    definition.JobType,
                    definition.MaxConcurrency,
                    definition.RetryCount,
                    definition.MaxExecutionTimeout);

                if (definition.JobType == JobType.Recurring)
                {
                    logger.LogDebug(
                        "Recurring job details - JobKey: {JobKey}, CronExpression: {CronExpression}, StartTime: {StartTime}, EndTime: {EndTime}, IsDisabled: {IsDisabled}",
                        definition.JobKey,
                        definition.CronExpression,
                        definition.StartTime,
                        definition.EndTime,
                        definition.IsDisabled);
                    continue;
                }

                if (definition.JobType != JobType.Triggered)
                {
                    continue;
                }

                var argumentsType = definition.JobArgsClrType
                    ?? throw new JobRegistrationException(
                        $"Triggered job '{definition.JobKey}' does not declare an argument CLR type.",
                        definition.JobKey);
                var argumentsKey = argumentsType.FullName
                    ?? throw new JobRegistrationException(
                        $"Argument CLR type '{argumentsType.Name}' for job '{definition.JobKey}' has no full name.",
                        definition.JobKey);

                triggeredJobTypesByArgsKey.TryAdd(argumentsKey, definition.JobClrType);
                triggeredArgsTypesByKey.TryAdd(argumentsKey, argumentsType);
                logger.LogDebug(
                    "Triggered job details - JobKey: {JobKey}, ParameterType: {ParameterType}",
                    definition.JobKey,
                    argumentsType.GetCleanFullName());
            }

            return new LocalJobCatalog(
                jobTypesByKey.ToFrozenDictionary(StringComparer.Ordinal),
                triggeredJobTypesByArgsKey.ToFrozenDictionary(StringComparer.Ordinal),
                triggeredArgsTypesByKey.ToFrozenDictionary(StringComparer.Ordinal));
        }
    }
}
