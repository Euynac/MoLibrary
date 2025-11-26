using MoLibrary.JobScheduler.Jobs;

namespace MoLibrary.JobScheduler.Models;

/// <summary>
/// Represents a single execution instance of a job.
/// Job instances track the lifecycle of an individual job execution from creation to completion.
/// </summary>
public class JobInstance
{
    /// <summary>
    /// Gets or sets the unique identifier for this job instance.
    /// Generated as a GUID when the instance is created.
    /// Used as the cancellation token key in IMoCancellationManager for distributed cancellation.
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the job key referencing the parent JobDefinition.
    /// Links this instance to its job definition for configuration and metadata.
    /// </summary>
    public string JobKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current execution state of this job instance.
    /// State transitions follow the state machine defined in JobState enum.
    /// </summary>
    public JobState State { get; set; }

    /// <summary>
    /// Gets or sets the JSON-serialized parameters for triggered jobs.
    /// Only applicable for <see cref="MoTriggeredJob{TParam}"/>. Null for recurring jobs.
    /// Deserialized and passed to the job's ExecuteAsync method.
    /// </summary>
    public string? JobArgs { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this job instance was created.
    /// Set when the instance is first created in the metadata store.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when job execution started.
    /// Set when a worker transitions the job to Processing state.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when job execution completed.
    /// Set when the job reaches a terminal state (Succeeded, Terminated, Cancelled, Skipped).
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Gets or sets the state change history.
    /// Each line represents a state transition in the format: [yyyy-MM-dd HH:mm:ss] [oldstate->newstate] message
    /// </summary>
    public string? StateHistory { get; set; }

    /// <summary>
    /// Gets or sets the current retry attempt number.
    /// Starts at 0 for the initial execution. Increments with each retry.
    /// Used to determine if the job should retry or transition to Terminated.
    /// </summary>
    public int RetryAttempt { get; set; } = 0;

    /// <summary>
    /// Gets or sets the client ID of the worker that is currently processing this job instance.
    /// Null if the job is not currently running.
    /// Used to track which worker is handling the job.
    /// </summary>
    public string? RunningClientId { get; set; }
    
    
    /// <summary>
    /// Updates the state of a job instance with validation of state transitions.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the state transition is invalid.</exception>
    public void UpdateStateAsync(JobState newState,
        string? message = null, string? clientId = null)
    {

        var currentState = State;

        // Validate state transition
        if (!IsValidTransition(currentState, newState))
        {
            throw new InvalidOperationException($"Invalid state transition from {currentState} to {newState}");
        }

        // Update state
        State = newState;

        // Update timestamps based on new state
        var now = DateTime.UtcNow;
        switch (newState)
        {
            case JobState.Processing:
                StartedAt = now;
                RunningClientId = clientId ?? throw new InvalidOperationException("RunningClientId must be set when state is Processing");
                break;

            case JobState.Succeeded:
            case JobState.Failed:
            case JobState.Terminated:
            case JobState.Cancelled:
            case JobState.Skipped:
                // Terminal states (and Failed which may retry but we still timestamp it)
                CompletedAt = now;
                break;
        }
        
        // Append state history if message is provided
        if (!string.IsNullOrEmpty(message))
        {
            AppendStateHistory(currentState, newState, message, now);
        }
    }

    /// <summary>
    /// Appends a state change record to the state history.
    /// </summary>
    /// <param name="oldState">The previous state.</param>
    /// <param name="newState">The new state.</param>
    /// <param name="message">The message to record.</param>
    /// <param name="timestamp">The timestamp of the state change.</param>
    private void AppendStateHistory(JobState oldState, JobState newState, string message, DateTime timestamp)
    {
        var historyEntry = $"[{timestamp:yyyy-MM-dd HH:mm:ss}] [{oldState}->{newState}] {message}";

        if (string.IsNullOrEmpty(StateHistory))
        {
            StateHistory = historyEntry;
        }
        else
        {
            StateHistory += Environment.NewLine + historyEntry;
        }
    }

    /// <summary>
    /// Parses the state history string into a list of structured records.
    /// </summary>
    /// <returns>A list of state history records, or an empty list if no history exists.</returns>
    public List<StateHistoryRecord> GetStateHistoryRecords()
    {
        if (string.IsNullOrEmpty(StateHistory))
        {
            return [];
        }

        var records = new List<StateHistoryRecord>();
        var lines = StateHistory.Split([Environment.NewLine, "\n"], StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (TryParseHistoryLine(line, out var record))
            {
                records.Add(record);
            }
        }

        return records;
    }

    /// <summary>
    /// Attempts to parse a single history line into a StateHistoryRecord.
    /// </summary>
    /// <param name="line">The line to parse.</param>
    /// <param name="record">The parsed record, if successful.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    private static bool TryParseHistoryLine(string line, out StateHistoryRecord record)
    {
        record = null!;

        // Expected format: [yyyy-MM-dd HH:mm:ss] [oldstate->newstate] message
        // Find the closing bracket of timestamp
        var timestampEnd = line.IndexOf(']');
        if (timestampEnd < 0)
        {
            return false;
        }

        // Extract timestamp
        var timestampStr = line[1..timestampEnd];
        if (!DateTime.TryParseExact(timestampStr, "yyyy-MM-dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out var timestamp))
        {
            return false;
        }

        // Find the state transition part
        var stateStart = line.IndexOf('[', timestampEnd + 1);
        var stateEnd = line.IndexOf(']', stateStart + 1);
        if (stateStart < 0 || stateEnd < 0)
        {
            return false;
        }

        // Extract and parse state transition
        var stateTransition = line[(stateStart + 1)..stateEnd];
        var states = stateTransition.Split("->", StringSplitOptions.TrimEntries);
        if (states.Length != 2)
        {
            return false;
        }

        if (!Enum.TryParse<JobState>(states[0], out var oldState) ||
            !Enum.TryParse<JobState>(states[1], out var newState))
        {
            return false;
        }

        // Extract message (everything after the second closing bracket and space)
        var message = line[(stateEnd + 1)..].TrimStart();

        record = new StateHistoryRecord(timestamp, oldState, newState, message);
        return true;
    }

    /// <summary>
    /// Represents a single state change record in the job instance history.
    /// </summary>
    /// <param name="Timestamp">The timestamp when the state change occurred.</param>
    /// <param name="OldState">The previous state.</param>
    /// <param name="NewState">The new state.</param>
    /// <param name="Message">The message associated with this state change.</param>
    public record StateHistoryRecord(DateTime Timestamp, JobState OldState, JobState NewState, string Message);


    /// <summary>
    /// Validates whether a state transition is allowed according to the state machine.
    /// </summary>
    /// <param name="currentState">The current state.</param>
    /// <param name="newState">The desired new state.</param>
    /// <returns>True if the transition is valid, false otherwise.</returns>
    private static bool IsValidTransition(JobState currentState, JobState newState)
    {
        // Allow transition to same state (idempotent updates)
        if (currentState == newState)
        {
            return true;
        }

        return currentState switch
        {
            // Scheduled can transition to Enqueued or Cancelled
            JobState.Scheduled => newState is JobState.Enqueued or JobState.Cancelled,

            // Enqueued can transition to Processing, Skipped, or Cancelled
            JobState.Enqueued => newState is JobState.Processing or JobState.Skipped or JobState.Cancelled,

            // Processing can transition to Succeeded, Failed, or Cancelled
            JobState.Processing => newState is JobState.Succeeded or JobState.Failed or JobState.Cancelled,

            // Failed can transition to Processing (retry) or Terminated (no retries)
            JobState.Failed => newState is JobState.Processing or JobState.Terminated,

            // Terminal states cannot transition to any other state
            JobState.Succeeded => false,
            JobState.Terminated => false,
            JobState.Cancelled => false,
            JobState.Skipped => false,

            _ => false
        };
    }
}
