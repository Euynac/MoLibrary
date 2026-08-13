using Microsoft.Extensions.Logging;

namespace Monica.JobScheduler.Abstractions;

/// <summary>
/// Internal job execution log writer bound to a single running durable execution.
/// </summary>
internal sealed class JobExecutionLogWriter(
    string instanceId,
    Func<string, LogLevel, Exception?, CancellationToken, Task> writeAsync)
{
    public string InstanceId { get; } = instanceId;

    public Task WriteAsync(
        string message,
        LogLevel logLevel,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        return writeAsync(message, logLevel, exception, cancellationToken);
    }
}

/// <summary>
/// Internal contract for job base classes that can receive an execution log writer during invocation.
/// </summary>
internal interface IJobExecutionLogBindingTarget
{
    void BindExecutionLogWriter(JobExecutionLogWriter writer);

    void ClearExecutionLogWriter();
}
