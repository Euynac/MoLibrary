using System.Text.Json;
using Monica.Tool.Extensions;

namespace Monica.Core.Results.Internal;

internal sealed class ResultEnvelopeDeserializationErrorInfo
{
    public string ExceptionType { get; init; } = typeof(JsonException).FullName ?? nameof(JsonException);

    public string Message { get; init; } = string.Empty;

    public long? LineNumber { get; init; }

    public long? BytePositionInLine { get; init; }

    public string Details { get; init; } = string.Empty;

    public bool IsResponseContentTruncated { get; init; }

    public static ResultEnvelopeDeserializationErrorInfo Create(JsonException exception, ResultEnvelopeCapturedContent responseContent)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new ResultEnvelopeDeserializationErrorInfo
        {
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message,
            LineNumber = exception.LineNumber,
            BytePositionInLine = exception.BytePositionInLine,
            Details = exception.GetJsonErrorDetails(responseContent.Content),
            IsResponseContentTruncated = responseContent.IsTruncated
        };
    }
}
