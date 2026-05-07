using System.Diagnostics.Metrics;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;
using Monica.Core.ObservableInstance.Models;

namespace Monica.Core.HostedService.Metrics;

/// <summary>
/// Emits hosted-service state transition metrics and observes the current hosted-service state snapshot.
/// </summary>
internal sealed class HostedServiceMetrics
{
    private const string MODULE_TAG_NAME = "monica.module";
    private const string SERVICE_NAME_TAG_NAME = "service.name";
    private const string FROM_TAG_NAME = "from";
    private const string TO_TAG_NAME = "to";
    private const string OUTCOME_TAG_NAME = "outcome";
    private const string STATE_TAG_NAME = "state";
    private const string MODULE_UNKNOWN = "unknown";
    private const string STATE_UNKNOWN = "unknown";
    private const string OUTCOME_SUCCESS = "success";
    private const string OUTCOME_FAILED = "failed";

    private readonly Counter<long> _transitions;
    private readonly IMoHostedServiceRegistry _serviceRegistry;

    /// <summary>
    /// Initializes hosted-service observable instruments.
    /// </summary>
    public HostedServiceMetrics(IMeterFactory meterFactory, IMoHostedServiceRegistry serviceRegistry)
    {
        _serviceRegistry = serviceRegistry;
        var meter = meterFactory.Create(HostedServiceMetricNames.MeterName);

        _transitions = meter.CreateCounter<long>(
            HostedServiceMetricNames.Transitions,
            unit: "transitions",
            description: "State transitions recorded by Monica hosted services.");

        meter.CreateObservableUpDownCounter(
            HostedServiceMetricNames.State,
            ObserveHostedServiceStates,
            unit: "services",
            description: "Current Monica hosted-service state distribution.");
    }

    /// <summary>
    /// Starts observing transition events for a hosted service.
    /// </summary>
    public void Observe(IMoHostedService service)
    {
        service.RuntimeInfo.Tracker.StateChanged += stateChange => RecordTransition(service, stateChange);
    }

    private void RecordTransition(IMoHostedService service, ObservableStateEntry stateChange)
    {
        var from = stateChange.PreviousState is HostedServiceState previousState
            ? FormatState(previousState)
            : STATE_UNKNOWN;
        var to = stateChange.CurrentState is HostedServiceState currentState
            ? FormatState(currentState)
            : STATE_UNKNOWN;

        _transitions.Add(
            1,
            new KeyValuePair<string, object?>(MODULE_TAG_NAME, FormatModule(service.ServiceGroupId)),
            new KeyValuePair<string, object?>(SERVICE_NAME_TAG_NAME, service.ServiceName),
            new KeyValuePair<string, object?>(FROM_TAG_NAME, from),
            new KeyValuePair<string, object?>(TO_TAG_NAME, to),
            new KeyValuePair<string, object?>(OUTCOME_TAG_NAME, stateChange.Exception is null ? OUTCOME_SUCCESS : OUTCOME_FAILED));
    }

    private Measurement<int>[] ObserveHostedServiceStates()
    {
        return _serviceRegistry
            .GetAllServices()
            .GroupBy(
                service => new HostedServiceStateKey(
                    FormatModule(GetServiceModule(service)),
                    service.ServiceName,
                    FormatState(service.CurrentState)))
            .Select(group => new Measurement<int>(
                group.Count(),
                new KeyValuePair<string, object?>(MODULE_TAG_NAME, group.Key.Module),
                new KeyValuePair<string, object?>(SERVICE_NAME_TAG_NAME, group.Key.ServiceName),
                new KeyValuePair<string, object?>(STATE_TAG_NAME, group.Key.State)))
            .ToArray();
    }

    private static string? GetServiceModule(HostedServiceRuntimeInfo service)
    {
        return service.Tracker.GroupId;
    }

    private static string FormatModule(string? module)
    {
        return string.IsNullOrWhiteSpace(module) ? MODULE_UNKNOWN : module;
    }

    private static string FormatState(HostedServiceState state)
    {
        return state.ToString().ToLowerInvariant();
    }

    private readonly record struct HostedServiceStateKey(string Module, string ServiceName, string State);
}
