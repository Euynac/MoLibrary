using System.Collections.Immutable;
using Monica.Framework.Seeder.Abstractions;
using Monica.Framework.Seeder.Models;
using Monica.Framework.Seeder.Models.Internal;

namespace Monica.Framework.Seeder.Services.Support;

internal sealed class SeederState(SeederGraph graph, TimeProvider timeProvider) : ISeederState
{
    private readonly object _sync = new();
    private readonly Dictionary<Type, MutableSeederState> _seeders = graph.Nodes.ToDictionary(
        static node => node.SeederType,
        static _ => new MutableSeederState());
    private DateTimeOffset? _startedAtUtc;
    private DateTimeOffset? _completedAtUtc;

    public SeederStateSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return new SeederStateSnapshot
            {
                CapturedAtUtc = timeProvider.GetUtcNow(),
                StartedAtUtc = _startedAtUtc,
                CompletedAtUtc = _completedAtUtc,
                Seeders = graph.Nodes.Select(CreateSnapshot).ToImmutableArray()
            };
        }
    }

    public SeederStatus GetStatus(Type seederType)
    {
        lock (_sync)
        {
            return GetMutableState(seederType).Status;
        }
    }

    public void MarkSchedulerStarted()
    {
        lock (_sync)
        {
            _startedAtUtc ??= timeProvider.GetUtcNow();
        }
    }

    public void MarkRunning(Type seederType, int attempt)
    {
        lock (_sync)
        {
            var state = GetMutableState(seederType);
            state.Status = SeederStatus.Running;
            state.Attempts = attempt;
            state.StartedAtUtc ??= timeProvider.GetUtcNow();
            state.CompletedAtUtc = null;
            state.ErrorType = null;
            state.ErrorMessage = null;
        }
    }

    public void MarkSucceeded(Type seederType)
    {
        MarkTerminal(seederType, SeederStatus.Succeeded, null, null);
    }

    public void MarkFailed(Type seederType, Exception exception)
    {
        MarkTerminal(seederType, SeederStatus.Failed, exception.GetType().FullName, exception.Message);
    }

    public void MarkBlocked(Type seederType, IReadOnlyCollection<string> dependencyNames)
    {
        MarkTerminal(
            seederType,
            SeederStatus.Blocked,
            "SeederDependencyFailure",
            $"Blocked by unsuccessful dependencies: {string.Join(", ", dependencyNames)}.");
    }

    public void MarkCancelled(Type seederType, string message)
    {
        MarkTerminal(seederType, SeederStatus.Cancelled, typeof(OperationCanceledException).FullName, message);
    }

    public void MarkSchedulerCompleted()
    {
        lock (_sync)
        {
            _completedAtUtc ??= timeProvider.GetUtcNow();
        }
    }

    private SeederExecutionSnapshot CreateSnapshot(SeederDescriptor descriptor)
    {
        var state = GetMutableState(descriptor.SeederType);
        return new SeederExecutionSnapshot
        {
            SeederTypeName = descriptor.SeederTypeName,
            ExecutionMode = descriptor.ExecutionMode,
            Criticality = descriptor.Criticality,
            Status = state.Status,
            Attempts = state.Attempts,
            MaxAttempts = descriptor.MaxAttempts,
            Dependencies = descriptor.Dependencies
                .Select(dependency => graph.NodesByType[dependency].SeederTypeName)
                .ToImmutableArray(),
            StartedAtUtc = state.StartedAtUtc,
            CompletedAtUtc = state.CompletedAtUtc,
            ErrorType = state.ErrorType,
            ErrorMessage = state.ErrorMessage
        };
    }

    private void MarkTerminal(Type seederType, SeederStatus status, string? errorType, string? errorMessage)
    {
        lock (_sync)
        {
            var state = GetMutableState(seederType);
            state.Status = status;
            state.CompletedAtUtc = timeProvider.GetUtcNow();
            state.ErrorType = errorType;
            state.ErrorMessage = errorMessage;
        }
    }

    private MutableSeederState GetMutableState(Type seederType)
    {
        return _seeders.TryGetValue(seederType, out var state)
            ? state
            : throw new InvalidOperationException($"Seeder '{seederType.FullName}' does not belong to this host's graph.");
    }

    private sealed class MutableSeederState
    {
        public SeederStatus Status { get; set; }
        public int Attempts { get; set; }
        public DateTimeOffset? StartedAtUtc { get; set; }
        public DateTimeOffset? CompletedAtUtc { get; set; }
        public string? ErrorType { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
