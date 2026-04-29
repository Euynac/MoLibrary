# MoHostedService Development Guide

This guide provides comprehensive documentation for developing hosted services in Monica using `MoBackgroundService` with built-in observability.

**Source locations**:
- `Monica.Core/Features/HostedServices/MoBackgroundService.cs`
- `Monica.RegisterCentre/Core/CoordinatedLeaderService.cs`

## Class Hierarchy

```
BackgroundService (Microsoft.Extensions.Hosting)
    ↓
MoBackgroundService (implements IMoHostedService)
    ↓
CoordinatedLeaderService (for leader-aware services)
    ↓
Your service implementation
```

## MoBackgroundService Base Class

### Required Dependencies (Primary Constructor)

```csharp
public class MyHostedService(
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> options,
    ILogger<MyHostedService> logger,  // Optional, but recommended
    IMyDependency dependency          // Your additional dependencies
) : MoBackgroundService(observableManager, options, logger)
```

| Dependency | Required | Purpose |
|------------|----------|---------|
| `IObservableInstanceManager` | Yes | Creates and manages observable agents for state tracking |
| `IOptions<ModuleHostedServiceOption>` | Yes | Configuration for history size, heartbeat, fail-fast behavior |
| `ILogger<T>` | No | Passed to base class for internal logging (used by RecordState) |

### Abstract/Virtual Members

| Member | Type | Must Override | Purpose |
|--------|------|---------------|---------|
| `ServiceName` | abstract property | Yes | Unique identifier for the service |
| `ExecuteBackgroundAsync` | abstract method | Yes | Main background work loop |
| `HeartbeatInterval` | virtual property | No | Override to change heartbeat interval (default from options) |
| `MaxHistorySize` | virtual property | No | Override to change history buffer size (default from options) |
| `ConfigureStateLogLevels` | virtual method | No | Customize log level mappings for states |
| `OnHeartbeatAsync` | virtual method | No | Custom logic on each heartbeat tick |

## RecordState - The Core Pattern

### Key Principle

**Use `RecordState` instead of direct Logger calls** for observability. The base class already configures a Logger internally which `RecordState` uses, so direct logging would be redundant and inconsistent with the observability model.

### RecordState Signature

```csharp
protected void RecordState(
    string message,
    HostedServiceState? newState = null,
    Exception? exception = null,
    LogLevel? logLevel = null)
```

| Parameter | Purpose |
|-----------|---------|
| `message` | Descriptive message about the current operation or state |
| `newState` | Optional: Transition to a new `HostedServiceState` |
| `exception` | Optional: Exception to record (increments exception counter) |
| `logLevel` | Optional: Explicit log level (overrides state-based mapping) |

### Usage Examples

```csharp
// Basic logging with explicit LogLevel
RecordState("Operation started", logLevel: LogLevel.Information);
RecordState("Processing batch of 50 items", logLevel: LogLevel.Debug);
RecordState("Warning: Retrying after transient failure", logLevel: LogLevel.Warning);

// Error with exception
RecordState("Database connection failed", logLevel: LogLevel.Error, exception: ex);

// State transition with log
RecordState("Service degraded due to high latency", HostedServiceState.Degraded);

// State transition with explicit log level
RecordState("Entering critical section", HostedServiceState.Executing, logLevel: LogLevel.Information);

// DON'T do this (redundant):
// Logger.LogInformation("Operation started");  // Already handled by RecordState internally
```

## HostedServiceState Enum

| State | Default Log Level | Description |
|-------|-------------------|-------------|
| `NotStarted` | Debug | Initial state before startup |
| `Starting` | Debug | Service is initializing |
| `Running` | Information | Service started successfully, ready for work |
| `Executing` | Information | Actively performing background work |
| `Stopping` | Information | Graceful shutdown in progress |
| `Stopped` | Information | Service has stopped |
| `Degraded` | Warning | Service running but with reduced functionality |
| `Faulted` | Error | Service encountered a critical error |

### Custom Log Level Mappings

Override `ConfigureStateLogLevels` to customize mappings:

```csharp
protected override void ConfigureStateLogLevels(ObservableAgent agent)
{
    // Custom mappings
    agent.SetDebugStates(HostedServiceState.NotStarted);
    agent.SetInformationStates(
        HostedServiceState.Starting,  // Changed from Debug
        HostedServiceState.Running,
        HostedServiceState.Executing,
        HostedServiceState.Stopping,
        HostedServiceState.Stopped
    );
    agent.SetWarningStates(HostedServiceState.Degraded);
    agent.SetErrorStates(HostedServiceState.Faulted);
}
```

## Complete Implementation Example

Based on `JobZombieDetectorService` pattern:

```csharp
public class MyMonitorService(
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    ILogger<MyMonitorService> logger,
    IMyDependency dependency,
    IOptions<MyModuleOption> options
) : MoBackgroundService(observableManager, hostedServiceOptions, logger)
{
    private readonly MyModuleOption _options = options.Value;

    public override string ServiceName => nameof(MyMonitorService);

    // Optional: Override heartbeat interval
    public override TimeSpan? HeartbeatInterval => TimeSpan.FromSeconds(30);

    protected override async Task ExecuteBackgroundAsync(CancellationToken stoppingToken)
    {
        RecordState($"Monitor configured: Interval={_options.MonitorInterval}",
            logLevel: LogLevel.Information);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.MonitorInterval, stoppingToken);
                await DoMonitorCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                RecordState("Error during monitor cycle",
                    logLevel: LogLevel.Error, exception: ex);
            }
        }
    }

    private async Task DoMonitorCycleAsync(CancellationToken cancellationToken)
    {
        RecordState("Monitor cycle started", logLevel: LogLevel.Information);

        var result = await dependency.CheckHealthAsync(cancellationToken);

        if (result.HasIssues)
        {
            RecordState($"Found {result.IssueCount} issues", logLevel: LogLevel.Warning);

            foreach (var issue in result.Issues)
            {
                await HandleIssueAsync(issue, cancellationToken);
            }

            RecordState($"Processed {result.IssueCount} issues", logLevel: LogLevel.Warning);
        }
        else
        {
            RecordState("Monitor cycle completed: No issues found", logLevel: LogLevel.Information);
        }
    }

    private async Task HandleIssueAsync(Issue issue, CancellationToken cancellationToken)
    {
        try
        {
            RecordState($"Handling issue: {issue.Id}", logLevel: LogLevel.Debug);
            await dependency.ResolveAsync(issue, cancellationToken);
            RecordState($"Resolved issue: {issue.Id}", logLevel: LogLevel.Information);
        }
        catch (Exception ex)
        {
            RecordState($"Failed to resolve issue: {issue.Id}",
                logLevel: LogLevel.Error, exception: ex);
        }
    }
}
```

## Service Registration

```csharp
// In module's ConfigureServices
services.AddHostedService<MyMonitorService>();
```

## CoordinatedLeaderService (Advanced)

For services that should only run on one instance in a distributed cluster. Located in `Monica.RegisterCentre/Core/CoordinatedLeaderService.cs`.

### When to Use

- Background tasks that should only run on one node (e.g., scheduled cleanup, zombie detection)
- Services that coordinate work across a cluster
- Tasks that require exclusive access to shared resources

### Key Differences from MoBackgroundService

| Aspect | MoBackgroundService | CoordinatedLeaderService |
|--------|---------------------|--------------------------|
| Execution | All instances run | Only leader runs |
| Main method | `ExecuteBackgroundAsync` | `LeaderExecuteBackgroundAsync` |
| Initialization | Direct | After leader election |
| Lifecycle hooks | None | `LeaderInitializeAsync`, `OnLeaderLostAsync` |

### Required Dependencies

```csharp
public class MyLeaderService(
    ILeaderElectionService leaderService,
    IOptions<ModuleJobSchedulerOption> options,
    ILogger<MyLeaderService> logger,
    IServiceRegistrationCoordinator coordinator,
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IMyDependency dependency
) : CoordinatedLeaderService(leaderService, options, logger, coordinator, observableManager, hostedServiceOptions)
```

### Abstract/Virtual Members

| Member | Type | Purpose |
|--------|------|---------|
| `ServiceName` | abstract property | Unique identifier |
| `LeaderInitializeAsync` | abstract method | Called when service becomes leader |
| `LeaderExecuteBackgroundAsync` | virtual method | Background work (only runs on leader) |
| `OnLeaderLostAsync` | virtual method | Cleanup when losing leader status |
| `OnBeforeInitialization` | virtual method | Pre-initialization hook |
| `ShouldWaitForRegistration` | virtual property | Wait for RegisterCentre before starting |

### Implementation Example

```csharp
public class MyCleanupService(
    ILeaderElectionService leaderService,
    IOptions<ModuleJobSchedulerOption> options,
    ILogger<MyCleanupService> logger,
    IServiceRegistrationCoordinator coordinator,
    IObservableInstanceManager observableManager,
    IOptions<ModuleHostedServiceOption> hostedServiceOptions,
    IMyRepository repository
) : CoordinatedLeaderService(leaderService, options, logger, coordinator, observableManager, hostedServiceOptions)
{
    public override string ServiceName => nameof(MyCleanupService);

    protected override Task LeaderInitializeAsync(CancellationToken cancellationToken)
    {
        RecordState("Cleanup service initialized as leader", logLevel: LogLevel.Information);
        return Task.CompletedTask;
    }

    protected override async Task LeaderExecuteBackgroundAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromHours(1), cancellationToken);
                await CleanupOldRecordsAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                RecordState("Cleanup failed", logLevel: LogLevel.Error, exception: ex);
            }
        }
    }

    protected override Task OnLeaderLostAsync(LeaderLostReason reason)
    {
        RecordState($"Lost leader status: {reason}", logLevel: LogLevel.Information);
        return Task.CompletedTask;
    }

    private async Task CleanupOldRecordsAsync(CancellationToken cancellationToken)
    {
        RecordState("Starting cleanup cycle", logLevel: LogLevel.Information);
        var count = await repository.DeleteOldRecordsAsync(cancellationToken);
        RecordState($"Cleaned up {count} old records", logLevel: LogLevel.Information);
    }
}
```

## Observable Instance Manager

The `IObservableInstanceManager` provides centralized access to all observable agents. Useful for:

- Monitoring dashboards
- Health checks
- Debugging service states

```csharp
// Query all hosted service states
var allInstances = observableManager.GetAllInstances();

// Get instances with exceptions
var faultedInstances = observableManager.GetInstancesWithExceptions();

// Get instances by state
var runningServices = observableManager.GetInstancesByState(HostedServiceState.Running);
```

## Best Practices

1. **Always use `RecordState`** - Never use `Logger` directly for operational messages
2. **Use explicit `logLevel`** - Provides clarity and consistency in log output
3. **Record exceptions properly** - Pass exceptions to `RecordState` for tracking
4. **Handle `OperationCanceledException`** - Break cleanly from loops during shutdown
5. **Use meaningful messages** - Include context like counts, IDs, or durations
6. **State transitions sparingly** - Only change state for significant service lifecycle events
7. **Configure heartbeat** - Override `HeartbeatInterval` for health monitoring
