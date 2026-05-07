using System.Diagnostics.Metrics;
using Monica.DataChannel.Pipeline;

namespace Monica.DataChannel.Metrics;

/// <summary>
/// Owns DataChannel message metrics emitted by message-counting middleware.
/// </summary>
public sealed class MessageMetrics
{
    private const string DIRECTION_TAG_NAME = "direction";
    private const string RESULT_TAG_NAME = "result";
    private const string DIRECTION_IN = "in";
    private const string DIRECTION_OUT = "out";
    private const string RESULT_SUCCESS = "success";
    private const string RESULT_FAILED = "failed";

    private readonly Counter<long> _messages;

    /// <summary>
    /// Initializes DataChannel message instruments.
    /// </summary>
    public MessageMetrics(IMeterFactory meterFactory)
    {
        _messages = meterFactory
            .Create(DataChannelMetricNames.MeterName)
            .CreateCounter<long>(
                DataChannelMetricNames.Messages,
                unit: "messages",
                description: "Messages observed by Monica DataChannel middleware.");
    }

    /// <summary>
    /// Records a successfully processed message.
    /// </summary>
    public void RecordSuccess(ChannelSide source)
    {
        RecordMessage(source, RESULT_SUCCESS);
    }

    /// <summary>
    /// Records a message that matched the middleware's error-message classification.
    /// </summary>
    public void RecordFailure(ChannelSide source)
    {
        RecordMessage(source, RESULT_FAILED);
    }

    private void RecordMessage(ChannelSide source, string result)
    {
        _messages.Add(
            1,
            new KeyValuePair<string, object?>(DIRECTION_TAG_NAME, FormatDirection(source)),
            new KeyValuePair<string, object?>(RESULT_TAG_NAME, result));
    }

    private static string FormatDirection(ChannelSide source)
    {
        return source switch
        {
            ChannelSide.Inner => DIRECTION_OUT,
            ChannelSide.Outer => DIRECTION_IN,
            _ => source.ToString().ToLowerInvariant()
        };
    }
}
