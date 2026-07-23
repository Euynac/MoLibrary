using Monica.Modules;

namespace Monica.Dapr.Services;

/// <summary>
/// Validates and resolves the terminal dead-letter destination for a Dapr source topic.
/// </summary>
internal sealed class DaprDeadLetterTopicPolicy
{
    private readonly string? _topicSuffix;

    private DaprDeadLetterTopicPolicy(string? topicSuffix)
    {
        _topicSuffix = topicSuffix;
    }

    public static DaprDeadLetterTopicPolicy Create(ModuleDaprEventBusOption options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var suffix = options.DeadLetterTopicSuffix;
        if (suffix is null)
        {
            return new DaprDeadLetterTopicPolicy(null);
        }

        if (suffix.Length == 0 || suffix.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "The dead-letter topic suffix must contain non-whitespace characters and cannot include whitespace.",
                nameof(options.DeadLetterTopicSuffix));
        }

        return new DaprDeadLetterTopicPolicy(suffix);
    }

    public string? Resolve(string sourceTopic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTopic);

        if (_topicSuffix is null || sourceTopic.EndsWith(_topicSuffix, StringComparison.Ordinal))
        {
            return null;
        }

        return string.Concat(sourceTopic, _topicSuffix);
    }
}
