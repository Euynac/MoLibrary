namespace Monica.Core.Results.Models.Internal;

internal sealed class ResultEnvelopeExceptionInfo
{
    public string ExceptionType { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string[] Details { get; init; } = [];

    public static ResultEnvelopeExceptionInfo Create(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new ResultEnvelopeExceptionInfo
        {
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message,
            Details = exception.ToString().Split('\n')
        };
    }
}
