using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Monica.Core.Execution;
using Monica.Core.HostedService.Abstractions;
using Monica.Core.HostedService.Models;

namespace Monica.Core.HostedService.Services.Support;

internal static class HostedServiceExecutionAdapter
{
    private static readonly ConcurrentDictionary<
        (Type ComponentType, ExecutionPoint Point, Type InputType, Type ResultType),
        ExecutionDescriptor>
        DESCRIPTORS = new();

    public static async Task ExecuteLifecycleAsync(
        IServiceScopeFactory serviceScopeFactory,
        object service,
        string serviceName,
        ExecutionPoint point,
        HostedServiceLifecyclePhase phase,
        Func<Task> terminal,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var serviceType = service.GetType();
        var descriptor = DESCRIPTORS.GetOrAdd(
            (serviceType, point, typeof(ExecutionUnit), typeof(ExecutionUnit)),
            static key => CreateLifecycleDescriptor(key.ComponentType, key.Point));
        var context = CreateContext(
            descriptor,
            ExecutionUnit.Value,
            service,
            serviceName,
            phase,
            serviceType,
            cancellationToken);

        await scope.ServiceProvider.GetRequiredService<IExecutionPipeline>()
            .ExecuteAsync(context, async () =>
            {
                await terminal().ConfigureAwait(false);
                return ExecutionUnit.Value;
            })
            .ConfigureAwait(false);
    }

    public static async Task ExecuteWorkItemAsync<TWorkItem>(
        IServiceScopeFactory serviceScopeFactory,
        object service,
        string serviceName,
        CancellationToken cancellationToken)
        where TWorkItem : class, IHostedServiceWorkItem
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var workItem = scope.ServiceProvider.GetRequiredService<TWorkItem>();
        var descriptor = DESCRIPTORS.GetOrAdd(
            (typeof(TWorkItem), HostedServiceExecutionPoints.WorkItem, typeof(ExecutionUnit), typeof(ExecutionUnit)),
            static key => CreateWorkItemDescriptor(
                key.ComponentType,
                typeof(IHostedServiceWorkItem),
                typeof(ExecutionUnit),
                typeof(ExecutionUnit)));
        var context = CreateContext(
            descriptor,
            ExecutionUnit.Value,
            workItem,
            serviceName,
            HostedServiceLifecyclePhase.WorkItem,
            service.GetType(),
            cancellationToken);

        await scope.ServiceProvider.GetRequiredService<IExecutionPipeline>()
            .ExecuteAsync(context, async () =>
            {
                await workItem.ExecuteAsync(cancellationToken).ConfigureAwait(false);
                return ExecutionUnit.Value;
            })
            .ConfigureAwait(false);
    }

    public static async Task<TResult> ExecuteWorkItemAsync<TWorkItem, TInput, TResult>(
        IServiceScopeFactory serviceScopeFactory,
        object service,
        string serviceName,
        TInput input,
        CancellationToken cancellationToken)
        where TWorkItem : class, IHostedServiceWorkItem<TInput, TResult>
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var workItem = scope.ServiceProvider.GetRequiredService<TWorkItem>();
        var descriptor = DESCRIPTORS.GetOrAdd(
            (typeof(TWorkItem), HostedServiceExecutionPoints.WorkItem, typeof(TInput), typeof(TResult)),
            static key => CreateWorkItemDescriptor(
                key.ComponentType,
                typeof(IHostedServiceWorkItem<TInput, TResult>),
                typeof(TInput),
                typeof(TResult)));
        var context = CreateContext(
            descriptor,
            input,
            workItem,
            serviceName,
            HostedServiceLifecyclePhase.WorkItem,
            service.GetType(),
            cancellationToken);

        return await scope.ServiceProvider.GetRequiredService<IExecutionPipeline>()
            .ExecuteAsync(context, () => workItem.ExecuteAsync(input, cancellationToken))
            .ConfigureAwait(false);
    }

    private static ExecutionContext<TInput> CreateContext<TInput>(
        ExecutionDescriptor descriptor,
        TInput input,
        object target,
        string serviceName,
        HostedServiceLifecyclePhase phase,
        Type ownerType,
        CancellationToken cancellationToken)
    {
        var features = new ExecutionFeatureCollection();
        features.Set(new HostedServiceExecutionFeature(serviceName, phase, ownerType));
        return new ExecutionContext<TInput>(
            descriptor,
            input,
            target,
            cancellationToken,
            features);
    }

    private static ExecutionDescriptor CreateLifecycleDescriptor(Type serviceType, ExecutionPoint point)
    {
        var isStart = point == HostedServiceExecutionPoints.Start;
        var methodName = isStart ? nameof(IHostedService.StartAsync) : nameof(IHostedService.StopAsync);
        var entryMethod = serviceType.GetMethod(methodName, [typeof(CancellationToken)]);
        return new ExecutionDescriptor(
            point,
            $"{serviceType.FullName}.{methodName}",
            serviceType,
            entryMethod,
            typeof(ExecutionUnit),
            typeof(ExecutionUnit),
            isBusinessOperation: false,
            isLongRunning: false);
    }

    private static ExecutionDescriptor CreateWorkItemDescriptor(
        Type workItemType,
        Type contract,
        Type inputType,
        Type resultType)
    {
        var entryMethod = workItemType.GetInterfaceMap(contract).TargetMethods.Single();
        return new ExecutionDescriptor(
            HostedServiceExecutionPoints.WorkItem,
            $"{workItemType.FullName}.{entryMethod.Name}",
            workItemType,
            entryMethod,
            inputType,
            resultType,
            isBusinessOperation: true,
            isLongRunning: false);
    }
}
