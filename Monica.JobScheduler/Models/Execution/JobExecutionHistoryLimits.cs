namespace Monica.JobScheduler.Models.Execution;

/// <summary>
/// Applies one consistent storage boundary to execution history across scheduler store implementations.
/// </summary>
internal readonly record struct JobExecutionHistoryLimits
{
    internal const int DEFAULT_MAX_ENTRIES = 1000;
    internal const int DEFAULT_MAX_MESSAGE_LENGTH = 4096;

    internal JobExecutionHistoryLimits(int maxEntries, int maxMessageLength)
    {
        if (maxEntries < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxEntries),
                maxEntries,
                "The execution history entry limit must be greater than zero.");
        }

        if (maxMessageLength < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxMessageLength),
                maxMessageLength,
                "The execution history message limit must be greater than zero.");
        }

        MaxEntries = maxEntries;
        MaxMessageLength = maxMessageLength;
    }

    internal int MaxEntries { get; }

    internal int MaxMessageLength { get; }

    internal string NormalizeMessage(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.Length <= MaxMessageLength)
        {
            return message;
        }

        if (MaxMessageLength == 1)
        {
            return "…";
        }

        var contentLength = MaxMessageLength - 1;
        if (char.IsHighSurrogate(message[contentLength - 1])
            && char.IsLowSurrogate(message[contentLength]))
        {
            contentLength--;
        }

        return $"{message[..contentLength]}…";
    }
}
