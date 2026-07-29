namespace Monica.Core.Execution.Models.Internal;

internal static class ExecutionPipelineCatalogSnapshotFactory
{
    public static ExecutionTypeSnapshot CreateType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return new ExecutionTypeSnapshot(
            type.AssemblyQualifiedName ?? type.FullName ?? type.Name,
            type.FullName ?? type.Name);
    }

    public static ExecutionDescriptorSnapshot CreateDescriptor(ExecutionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new ExecutionDescriptorSnapshot(
            descriptor.OperationKey,
            descriptor.DisplayName,
            descriptor.Point,
            CreateType(descriptor.ComponentType),
            descriptor.ContractType is null ? null : CreateType(descriptor.ContractType),
            CreateMethodDisplayName(descriptor.EntryMethod),
            CreateType(descriptor.InputType),
            CreateType(descriptor.ResultType),
            descriptor.IsBusinessOperation,
            descriptor.TransactionMode);
    }

    private static string? CreateMethodDisplayName(System.Reflection.MethodInfo? method)
    {
        if (method is null)
        {
            return null;
        }

        var declaringType = method.DeclaringType?.FullName;
        return string.IsNullOrWhiteSpace(declaringType)
            ? method.ToString()
            : $"{declaringType}.{method}";
    }
}
