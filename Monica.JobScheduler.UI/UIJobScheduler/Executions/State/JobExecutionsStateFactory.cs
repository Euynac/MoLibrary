using Microsoft.Extensions.Options;
using Monica.JobScheduler.Facades;
using Monica.JobScheduler.UI.UIJobScheduler.Shared;
using Monica.Modules;

namespace Monica.JobScheduler.UI.UIJobScheduler.Executions.State;

/// <summary>
/// Creates component-owned execution ledger and detail states from circuit-scoped scheduler dependencies.
/// </summary>
internal sealed class JobExecutionsStateFactory(
    JobSchedulerFacade facade,
    IJobSchedulerUiAccess access,
    IOptions<ModuleJobSchedulerUIOption> options,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Creates a fresh state for one execution-ledger page instance.
    /// </summary>
    public JobExecutionsPageState CreatePageState()
    {
        return new JobExecutionsPageState(facade, access, options.Value.DefaultPageSize, timeProvider);
    }

    /// <summary>
    /// Creates a fresh state for one execution-detail dialog instance.
    /// </summary>
    public ExecutionDetailState CreateDetailState()
    {
        return new ExecutionDetailState(facade, access, timeProvider);
    }
}
