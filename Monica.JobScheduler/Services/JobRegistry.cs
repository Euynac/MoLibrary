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
/// The local catalog is built once when this worker singleton is created. Durable owner snapshots never mutate the
/// process-local CLR-type catalog.
/// </remarks>
public class JobRegistry(
    IReadOnlyList<LocalJobDefinition> localJobDefinitions,
    ILogger<JobRegistry> logger)
{
    private readonly LocalJobCatalog _localJobs = LocalJobCatalog.Create(localJobDefinitions, logger);

    /// <summary>
    /// Retrieves the CLR type for a job by its job key.
    /// </summary>
    /// <param name="jobKey">The unique job key (job type's full name).</param>
    /// <returns>The CLR type of the job, or null if not found.</returns>
    public Type? GetJobClrType(string jobKey) => _localJobs.JobTypesByKey.GetValueOrDefault(jobKey);

    /// <summary>
    /// Retrieves the CLR type for job arguments by its job args key.
    /// </summary>
    /// <param name="jobArgsKey">The unique job args key (args type's full name).</param>
    /// <returns>The CLR type of the job arguments, or null if not found.</returns>
    public Type? GetJobArgsClrType(string jobArgsKey) =>
        _localJobs.TriggeredArgsTypesByKey.GetValueOrDefault(jobArgsKey);

    private sealed record LocalJobCatalog(
        FrozenDictionary<string, Type> JobTypesByKey,
        FrozenDictionary<string, Type> TriggeredArgsTypesByKey)
    {
        internal static LocalJobCatalog Create(
            IReadOnlyList<LocalJobDefinition> definitions,
            ILogger<JobRegistry> logger)
        {
            ArgumentNullException.ThrowIfNull(definitions);

            var jobTypesByKey = new Dictionary<string, Type>(StringComparer.Ordinal);
            var triggeredArgsTypesByKey = new Dictionary<string, Type>(StringComparer.Ordinal);

            foreach (var definition in definitions)
            {
                var declaration = definition.Declaration;
                if (!jobTypesByKey.TryAdd(declaration.JobKey, definition.JobClrType))
                {
                    throw new JobRegistrationException(
                        $"Duplicate local job key '{declaration.JobKey}'.",
                        declaration.JobKey);
                }

                logger.LogInformation(
                    "Registered local job: {JobKey} | Name: {JobName} | Type: {JobType} | MaxConcurrency: {MaxConcurrency} | RetryCount: {RetryCount} | Timeout: {Timeout}",
                    declaration.JobKey,
                    declaration.JobName,
                    declaration.JobType,
                    declaration.MaxConcurrency,
                    declaration.RetryCount,
                    declaration.MaxExecutionTimeout);

                if (declaration.JobType == JobType.Recurring)
                {
                    logger.LogDebug(
                        "Recurring job details - JobKey: {JobKey}, CronExpression: {CronExpression}, StartTime: {StartTime}, EndTime: {EndTime}, IsDisabled: {IsDisabled}",
                        declaration.JobKey,
                        declaration.CronExpression,
                        declaration.StartTimeUtc,
                        declaration.EndTimeUtc,
                        declaration.IsDisabledByDefault);
                    continue;
                }

                if (declaration.JobType != JobType.Triggered)
                {
                    continue;
                }

                var argumentsType = definition.JobArgsClrType
                    ?? throw new JobRegistrationException(
                        $"Triggered job '{declaration.JobKey}' does not declare an argument CLR type.",
                        declaration.JobKey);
                var argumentsKey = argumentsType.FullName
                    ?? throw new JobRegistrationException(
                        $"Argument CLR type '{argumentsType.Name}' for job '{declaration.JobKey}' has no full name.",
                        declaration.JobKey);

                triggeredArgsTypesByKey.TryAdd(argumentsKey, argumentsType);
                logger.LogDebug(
                    "Triggered job details - JobKey: {JobKey}, ParameterType: {ParameterType}",
                    declaration.JobKey,
                    argumentsType.GetCleanFullName());
            }

            return new LocalJobCatalog(
                jobTypesByKey.ToFrozenDictionary(StringComparer.Ordinal),
                triggeredArgsTypesByKey.ToFrozenDictionary(StringComparer.Ordinal));
        }
    }
}
