using Microsoft.Extensions.DependencyInjection;

namespace Monica.Core.Execution.Models.Internal;

internal sealed class ExecutionBehaviorRegistration
{
    private static readonly Type BEHAVIOR_INTERFACE_TYPE = typeof(IExecutionBehavior<,>);
    private readonly HashSet<(Type InputType, Type ResultType)> _closedContracts;

    private ExecutionBehaviorRegistration(
        string key,
        Type implementationType,
        int order,
        Func<ExecutionDescriptor, bool> descriptorFilter,
        ServiceLifetime lifetime,
        HashSet<(Type InputType, Type ResultType)> closedContracts,
        bool isOpenGeneric)
    {
        Key = key;
        ImplementationType = implementationType;
        Order = order;
        DescriptorFilter = descriptorFilter;
        Lifetime = lifetime;
        _closedContracts = closedContracts;
        IsOpenGeneric = isOpenGeneric;
    }

    public string Key { get; }

    public Type ImplementationType { get; }

    public int Order { get; }

    private Func<ExecutionDescriptor, bool> DescriptorFilter { get; }

    private ServiceLifetime Lifetime { get; }

    private bool IsOpenGeneric { get; }

    public static ExecutionBehaviorRegistration Create(
        string key,
        Type implementationType,
        int order,
        Func<ExecutionDescriptor, bool>? descriptorFilter,
        ServiceLifetime lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(implementationType);

        if (!string.Equals(key, key.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Execution behavior keys must not contain leading or trailing whitespace.",
                nameof(key));
        }

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
            key,
            implementationType,
            order,
            descriptorFilter ?? (static _ => true),
            lifetime,
            closedContracts,
            isOpenGeneric);
    }

    public ServiceDescriptor CreateServiceDescriptor()
    {
        return ServiceDescriptor.DescribeKeyed(
            ImplementationType,
            Key,
            ImplementationType,
            Lifetime);
    }

    public ExecutionBehaviorPlan? TryCreatePlan(
        ExecutionDescriptor descriptor,
        Type inputType,
        Type resultType)
    {
        if (!DescriptorFilter(descriptor))
        {
            return null;
        }

        var implementationType = TryCloseImplementation(inputType, resultType);
        return implementationType is null
            ? null
            : new ExecutionBehaviorPlan(Key, implementationType);
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
