using System.Text;
using Monica.JobScheduler.Jobs;

namespace Monica.JobScheduler.Models;

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
    /// Gets or sets the scheduled execution time for delayed jobs.
    /// Set when a job is created with a delay via IMoTriggeredJobManager.EnqueueAsync.
    /// Null for immediate execution or recurring jobs.
    /// Used during service restart to calculate remaining delay and reschedule.
    /// </summary>
    public DateTime? ScheduledExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the state change history.
    /// Uses line-prefix format where each entry starts with ">>> " followed by metadata:
    /// >>> [yyyy-MM-dd HH:mm:ss] [oldstate->newstate] optional message
    /// Multi-line messages (like stack traces) continue on following lines without the ">>> " prefix.
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
        
        // Always append state history for complete audit trail
        AppendStateHistory(currentState, newState, message, now);
    }

    /// <summary>
    /// Appends a state change record to the state history using line-prefix format.
    /// Format: >>> [timestamp] [oldstate->newstate] optional message
    /// Multi-line messages continue on subsequent lines without the prefix.
    /// </summary>
    /// <param name="oldState">The previous state.</param>
    /// <param name="newState">The new state.</param>
    /// <param name="message">Optional message to record. Can be null or multi-line.</param>
    /// <param name="timestamp">The timestamp of the state change.</param>
    private void AppendStateHistory(JobState oldState, JobState newState, string? message, DateTime timestamp)
    {
        var header = $">>> [{timestamp:yyyy-MM-dd HH:mm:ss}] [{oldState}->{newState}]";
        var historyEntry = string.IsNullOrEmpty(message)
            ? header
            : $"{header} {message}";

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
    /// Handles line-prefix format where entries start with ">>> ".
    /// </summary>
    /// <returns>A list of state history records, or an empty list if no history exists.</returns>
    public List<StateHistoryRecord> GetStateHistoryRecords()
    {
        if (string.IsNullOrEmpty(StateHistory))
        {
            return [];
        }

        var records = new List<StateHistoryRecord>();
        var entries = SplitIntoEntries(StateHistory);

        foreach (var entry in entries)
        {
            if (TryParseHistoryEntry(entry, out var record))
            {
                records.Add(record);
            }
        }

        return records;
    }

    /// <summary>
    /// Splits the state history into individual entries based on the ">>> " line prefix.
    /// Each entry may contain multiple lines if the message is multi-line.
    /// </summary>
    /// <param name="history">The complete state history string.</param>
    /// <returns>A list of individual entry strings.</returns>
    private static List<string> SplitIntoEntries(string history)
    {
        var entries = new List<string>();
        var lines = history.Split(["\r\n", "\n"], StringSplitOptions.None);
        var currentEntry = new StringBuilder();

        foreach (var line in lines)
        {
            if (line.StartsWith(">>>"))
            {
                // Start of new entry - save previous if exists
                if (currentEntry.Length > 0)
                {
                    entries.Add(currentEntry.ToString().TrimEnd());
                    currentEntry.Clear();
                }
                currentEntry.AppendLine(line);
            }
            else if (currentEntry.Length > 0)
            {
                // Continuation line for current entry
                currentEntry.AppendLine(line);
            }
        }

        // Add final entry
        if (currentEntry.Length > 0)
        {
            entries.Add(currentEntry.ToString().TrimEnd());
        }

        return entries;
    }

    /// <summary>
    /// Attempts to parse a single history entry into a StateHistoryRecord.
    /// Handles multi-line entries where the first line has metadata and subsequent lines are message continuation.
    /// </summary>
    /// <param name="entry">The entry to parse (may contain multiple lines).</param>
    /// <param name="record">The parsed record, if successful.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    private static bool TryParseHistoryEntry(string entry, out StateHistoryRecord record)
    {
        record = null!;

        // Entry format:
        // >>> [timestamp] [oldstate->newstate] optional message
        // continuation line 1
        // continuation line 2

        var lines = entry.Split(["\r\n", "\n"], StringSplitOptions.None);
        if (lines.Length == 0 || !lines[0].StartsWith(">>>"))
        {
            return false;
        }

        var firstLine = lines[0][4..]; // Skip ">>> "

        // Parse timestamp
        var timestampEnd = firstLine.IndexOf(']');
        if (timestampEnd < 0 || firstLine[0] != '[')
        {
            return false;
        }

        var timestampStr = firstLine[1..timestampEnd];
        if (!DateTime.TryParseExact(timestampStr, "yyyy-MM-dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out var timestamp))
        {
            return false;
        }

        // Parse state transition
        var stateStart = firstLine.IndexOf('[', timestampEnd);
        var stateEnd = firstLine.IndexOf(']', stateStart + 1);
        if (stateStart < 0 || stateEnd < 0)
        {
            return false;
        }

        var stateTransition = firstLine[(stateStart + 1)..stateEnd];
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

        // Extract message (rest of first line + all continuation lines)
        var messageBuilder = new StringBuilder();
        var firstLineMessage = stateEnd + 1 < firstLine.Length
            ? firstLine[(stateEnd + 1)..].TrimStart()
            : "";

        if (!string.IsNullOrEmpty(firstLineMessage))
        {
            messageBuilder.Append(firstLineMessage);
        }

        // Add continuation lines
        for (int i = 1; i < lines.Length; i++)
        {
            if (messageBuilder.Length > 0)
            {
                messageBuilder.AppendLine();
            }
            messageBuilder.Append(lines[i]);
        }

        var message = messageBuilder.ToString();
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
        return currentState switch
        {
            // Scheduled can transition to Enqueued, Cancelled, or Failed
            JobState.Scheduled => newState is JobState.Enqueued or JobState.Cancelled or JobState.Failed,

            // Enqueued can transition to Processing, Skipped, or Cancelled or Failed (delivery failure)
            JobState.Enqueued => newState is JobState.Processing or JobState.Skipped or JobState.Cancelled or JobState.Failed,

            // Processing can transition to Succeeded, Failed, or Cancelled
            JobState.Processing => newState is JobState.Succeeded or JobState.Failed or JobState.Cancelled,

            // Failed can transition to Processing (retry) or Terminated (no retries)
            JobState.Failed => newState is JobState.Processing or JobState.Terminated or JobState.Failed,

            // Terminal states cannot transition to any other state
            JobState.Succeeded => false,
            JobState.Terminated => false,
            JobState.Cancelled => false,
            JobState.Skipped => false,

            _ => false
        };
    }

    /// <summary>
    /// Returns a string representation of the job instance.
    /// </summary>
    /// <returns>A string containing key information about the job instance.</returns>
    public override string ToString()
    {
        var retryInfo = RetryAttempt > 0 ? $" (Retry: {RetryAttempt})" : "";
        var clientInfo = !string.IsNullOrEmpty(RunningClientId) ? $" [{RunningClientId}]" : "";
        return $"JobInstance[{InstanceId}] {JobKey} - {State}{retryInfo}{clientInfo}";
    }
}
