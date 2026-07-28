using Microsoft.Extensions.DependencyInjection;

namespace Monica.Core.Execution.Models.Internal;

internal sealed class ExecutionBehaviorRegistration
{
    private static readonly Type BEHAVIOR_INTERFACE_TYPE = typeof(IExecutionBehavior<,>);
    private readonly HashSet<(Type InputType, Type ResultType)> _closedContracts;

    private ExecutionBehaviorRegistration(
        Type implementationType,
        int order,
        Func<ExecutionDescriptor, bool> descriptorFilter,
        ServiceLifetime lifetime,
        HashSet<(Type InputType, Type ResultType)> closedContracts,
        bool isOpenGeneric)
    {
        ImplementationType = implementationType;
        SortName = implementationType.AssemblyQualifiedName
                   ?? implementationType.FullName
                   ?? implementationType.Name;
        Order = order;
        DescriptorFilter = descriptorFilter;
        Lifetime = lifetime;
        _closedContracts = closedContracts;
        IsOpenGeneric = isOpenGeneric;
    }

    public Type ImplementationType { get; }

    public string SortName { get; }

    public int Order { get; }

    private Func<ExecutionDescriptor, bool> DescriptorFilter { get; }

    private ServiceLifetime Lifetime { get; }

    private bool IsOpenGeneric { get; }

    public static ExecutionBehaviorRegistration Create(
        Type implementationType,
        int order,
        Func<ExecutionDescriptor, bool>? descriptorFilter,
        ServiceLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(implementationType);

        if (implementationType is not { IsClass: true, IsAbstract: false })
        {
            throw new ArgumentException(
                $"Execution behavior '{implementationType.FullName}' must be a non-abstract class.",
                nameof(implementationType));
        }

        if (!Enum.IsDefined(lifetime))
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Unsupported service lifetime.");
        }

        var behaviorContracts = implementationType
            .GetInterfaces()
            .Where(IsBehaviorContract)
            .ToArray();

        if (behaviorContracts.Length == 0)
        {
            throw new ArgumentException(
                $"Execution behavior '{implementationType.FullName}' does not implement " +
                $"{BEHAVIOR_INTERFACE_TYPE.Name}.",
                nameof(implementationType));
        }

        var isOpenGeneric = implementationType.IsGenericTypeDefinition;
        var closedContracts = new HashSet<(Type InputType, Type ResultType)>();

        if (isOpenGeneric)
        {
            ValidateOpenGenericContract(implementationType, behaviorContracts);
        }
        else
        {
            if (implementationType.ContainsGenericParameters)
            {
                throw new ArgumentException(
                    $"Execution behavior '{implementationType.FullName}' must be a closed type or a generic type definition.",
                    nameof(implementationType));
            }

            foreach (var behaviorContract in behaviorContracts)
            {
                var arguments = behaviorContract.GetGenericArguments();
                closedContracts.Add((arguments[0], arguments[1]));
            }
        }

        return new ExecutionBehaviorRegistration(
            implementationType,
            order,
            descriptorFilter ?? (static _ => true),
            lifetime,
            closedContracts,
            isOpenGeneric);
    }

    public ServiceDescriptor CreateServiceDescriptor()
    {
        return ServiceDescriptor.Describe(
            ImplementationType,
            ImplementationType,
            Lifetime);
    }

    public void ValidateExclusiveServiceRegistration(IServiceCollection services)
    {
        var registrations = services
            .Where(descriptor => descriptor.ServiceType == ImplementationType)
            .ToArray();

        if (registrations.Length == 1)
        {
            var registration = registrations[0];
            if (registration.ImplementationType == ImplementationType &&
                registration.ImplementationFactory is null &&
                registration.ImplementationInstance is null &&
                registration.Lifetime == Lifetime)
            {
                return;
            }
        }

        throw new InvalidOperationException(
            $"Execution behavior '{ImplementationType.FullName}' must have exactly one service registration, owned " +
            "by AddBehavior. Do not register the behavior implementation separately in the host service collection.");
    }

    public Type? TryCreatePlan(
        ExecutionDescriptor descriptor,
        Type inputType,
        Type resultType)
    {
        if (!DescriptorFilter(descriptor))
        {
            return null;
        }

        return TryCloseImplementation(inputType, resultType);
    }

    private Type? TryCloseImplementation(Type inputType, Type resultType)
    {
        if (!IsOpenGeneric)
        {
            return _closedContracts.Contains((inputType, resultType))
                ? ImplementationType
                : null;
        }

        try
        {
            var closedType = ImplementationType.MakeGenericType(inputType, resultType);
            var closedContract = BEHAVIOR_INTERFACE_TYPE.MakeGenericType(inputType, resultType);
            return closedContract.IsAssignableFrom(closedType) ? closedType : null;
        }
        catch (ArgumentException)
        {
            // Generic constraints are a natural way for an open behavior to limit the signatures it supports.
            return null;
        }
    }

    private static bool IsBehaviorContract(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == BEHAVIOR_INTERFACE_TYPE;
    }

    private static void ValidateOpenGenericContract(Type implementationType, IReadOnlyCollection<Type> behaviorContracts)
    {
        var implementationArguments = implementationType.GetGenericArguments();
        if (implementationArguments.Length != 2 ||
            !behaviorContracts.Any(contract =>
                contract.GetGenericArguments().SequenceEqual(implementationArguments)))
        {
            throw new ArgumentException(
                $"Open execution behavior '{implementationType.FullName}' must declare exactly two generic parameters " +
                $"and implement {BEHAVIOR_INTERFACE_TYPE.Name} with those parameters in input/result order.",
                nameof(implementationType));
        }
    }
}
