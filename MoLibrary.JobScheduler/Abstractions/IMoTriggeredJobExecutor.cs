using MoLibrary.JobScheduler.Models;

namespace MoLibrary.JobScheduler.Abstractions;

public interface IMoTriggeredJobExecutor
{
    Task ExecuteAsync(JobExecutionContext context);
}