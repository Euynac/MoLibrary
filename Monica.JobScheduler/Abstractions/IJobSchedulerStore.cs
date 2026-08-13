namespace Monica.JobScheduler.Abstractions;

/// <summary>
/// Provides the single durable correctness boundary for catalog and execution state.
/// </summary>
public interface IJobSchedulerStore : IJobCatalogStore, IJobExecutionStore;
