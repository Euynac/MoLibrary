using Monica.Framework.Seeder.Models;

namespace Test.Monica.Framework.UI.Seeder;

internal static class SeederUiTestData
{
    private static readonly DateTimeOffset STARTED_AT = DateTimeOffset.Parse("2026-08-10T01:00:00Z");

    internal static SeederDiagnosticsSnapshot CreateSnapshot(
        SeederRunStatus runStatus = SeederRunStatus.Succeeded,
        IReadOnlyList<SeederExecutionSnapshot>? seeders = null)
    {
        var entries = seeders?.ToArray() ?? CreateDefaultSeeders(runStatus);
        var isTerminal = runStatus is SeederRunStatus.Succeeded or
            SeederRunStatus.CompletedWithFailures or
            SeederRunStatus.Aborted or
            SeederRunStatus.Cancelled;
        var failFastTrigger = runStatus is SeederRunStatus.Aborting or SeederRunStatus.Aborted
            ? entries.FirstOrDefault(static seeder =>
                seeder.Status == SeederStatus.Failed &&
                seeder.FailureBehavior == SeederFailureBehavior.FailFast)?.SeederTypeName
            : null;

        if ((runStatus is SeederRunStatus.Aborting or SeederRunStatus.Aborted) && failFastTrigger is null)
        {
            throw new InvalidOperationException("An aborting Seeder fixture requires a failed FailFast trigger.");
        }

        return new SeederDiagnosticsSnapshot
        {
            Configuration = new SeederConfigurationSnapshot
            {
                MaxConcurrency = 4,
                DefaultExecutionMode = SeederExecutionMode.Concurrent,
                DefaultCriticality = SeederCriticality.Required,
                DefaultMaxAttempts = 3,
                RetryBaseDelay = TimeSpan.FromMilliseconds(200),
                RetryMaxDelay = TimeSpan.FromSeconds(2),
                DefaultFailureBehavior = SeederFailureBehavior.ContinueAndRecord
            },
            Run = new SeederStateSnapshot
            {
                CapturedAtUtc = STARTED_AT.AddSeconds(3),
                Status = runStatus,
                StartedAtUtc = runStatus == SeederRunStatus.Waiting ? null : STARTED_AT,
                CompletedAtUtc = isTerminal ? STARTED_AT.AddSeconds(3) : null,
                Duration = runStatus == SeederRunStatus.Waiting ? null : TimeSpan.FromSeconds(3),
                FailFastTriggerSeederTypeName = failFastTrigger,
                Seeders = [.. entries]
            }
        };
    }

    internal static SeederExecutionSnapshot CreateSeeder(
        string name = "CatalogSeeder",
        SeederStatus status = SeederStatus.Succeeded,
        SeederCriticality criticality = SeederCriticality.Required,
        SeederFailureBehavior failureBehavior = SeederFailureBehavior.ContinueAndRecord,
        IReadOnlyList<string>? dependencies = null,
        string? errorMessage = null)
    {
        var effectiveDependencies = dependencies ?? (status == SeederStatus.Blocked
            ? ["Example.Seeders.FailedDependencySeeder"]
            : []);
        var hasAttempt = status is SeederStatus.Running or
            SeederStatus.Succeeded or
            SeederStatus.Failed or
            SeederStatus.Cancelled;
        var terminal = status is SeederStatus.Succeeded or SeederStatus.Failed or
            SeederStatus.Blocked or SeederStatus.Cancelled;
        var attemptStatus = status switch
        {
            SeederStatus.Succeeded => SeederAttemptStatus.Succeeded,
            SeederStatus.Failed => SeederAttemptStatus.Failed,
            SeederStatus.Cancelled => SeederAttemptStatus.Cancelled,
            _ => SeederAttemptStatus.Running
        };
        var diagnostic = CreateDiagnostic(status, effectiveDependencies, errorMessage);

        return new SeederExecutionSnapshot
        {
            SeederName = name,
            SeederTypeName = $"Example.Seeders.{name}",
            ExecutionMode = SeederExecutionMode.Concurrent,
            ExecutionModeSource = SeederPolicySource.ModuleDefault,
            Criticality = criticality,
            CriticalitySource = criticality == SeederCriticality.Required
                ? SeederPolicySource.ModuleDefault
                : SeederPolicySource.SeederOverride,
            FailureBehavior = failureBehavior,
            FailureBehaviorSource = failureBehavior == SeederFailureBehavior.ContinueAndRecord
                ? SeederPolicySource.ModuleDefault
                : SeederPolicySource.SeederOverride,
            Status = status,
            Attempts = hasAttempt ? 1 : 0,
            MaxAttempts = 3,
            MaxAttemptsSource = SeederPolicySource.SeederOverride,
            Dependencies = [.. effectiveDependencies],
            StartedAtUtc = hasAttempt ? STARTED_AT : null,
            CompletedAtUtc = terminal ? STARTED_AT.AddSeconds(3) : null,
            Duration = hasAttempt ? TimeSpan.FromSeconds(3) : null,
            AttemptHistory = hasAttempt
                ?
                [
                    new SeederAttemptSnapshot
                    {
                        AttemptNumber = 1,
                        Status = attemptStatus,
                        StartedAtUtc = STARTED_AT,
                        CompletedAtUtc = terminal ? STARTED_AT.AddSeconds(3) : null,
                        Duration = TimeSpan.FromSeconds(3),
                        ErrorType = attemptStatus is SeederAttemptStatus.Failed or SeederAttemptStatus.Cancelled
                            ? diagnostic.ErrorType
                            : null,
                        ErrorMessage = attemptStatus is SeederAttemptStatus.Failed or SeederAttemptStatus.Cancelled
                            ? diagnostic.ErrorMessage
                            : null
                    }
                ]
                : [],
            ErrorType = diagnostic.ErrorType,
            ErrorMessage = diagnostic.ErrorMessage
        };
    }

    private static SeederExecutionSnapshot[] CreateDefaultSeeders(SeederRunStatus runStatus)
    {
        return runStatus switch
        {
            SeederRunStatus.Waiting => [CreateSeeder(status: SeederStatus.Pending)],
            SeederRunStatus.Running => [CreateSeeder(status: SeederStatus.Running)],
            SeederRunStatus.Aborting =>
            [
                CreateSeeder(
                    status: SeederStatus.Failed,
                    failureBehavior: SeederFailureBehavior.FailFast,
                    errorMessage: "Seeder execution failed."),
                CreateSeeder("ProjectionSeeder", SeederStatus.Running)
            ],
            SeederRunStatus.Succeeded => [CreateSeeder()],
            SeederRunStatus.CompletedWithFailures =>
                [CreateSeeder(status: SeederStatus.Failed, errorMessage: "Seeder execution failed.")],
            SeederRunStatus.Aborted =>
                [CreateSeeder(
                    status: SeederStatus.Failed,
                    failureBehavior: SeederFailureBehavior.FailFast,
                    errorMessage: "Seeder execution failed.")],
            SeederRunStatus.Cancelled => [CreateSeeder(status: SeederStatus.Cancelled)],
            _ => throw new ArgumentOutOfRangeException(nameof(runStatus), runStatus, "Unknown Seeder run status.")
        };
    }

    private static (string? ErrorType, string? ErrorMessage) CreateDiagnostic(
        SeederStatus status,
        IReadOnlyList<string> dependencies,
        string? errorMessage)
    {
        return status switch
        {
            SeederStatus.Failed => (
                "System.InvalidOperationException",
                errorMessage ?? "Seeder execution failed."),
            SeederStatus.Blocked => (
                "SeederDependencyFailure",
                errorMessage ?? $"Blocked by unsuccessful dependencies: {string.Join(", ", dependencies)}."),
            SeederStatus.Cancelled => (
                "System.OperationCanceledException",
                errorMessage ?? "Seeder execution was cancelled."),
            _ => (null, null)
        };
    }
}
