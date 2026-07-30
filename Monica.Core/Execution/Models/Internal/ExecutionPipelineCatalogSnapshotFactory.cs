using System.Reflection;
using Monica.Tool.Extensions;

namespace Monica.Core.Execution.Models.Internal;

internal static class ExecutionPipelineCatalogSnapshotFactory
{
    public static ExecutionTypeSnapshot CreateType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return new ExecutionTypeSnapshot(
            type.GetCleanName(),
            type.GetCleanFullName(),
            type.Assembly.GetName().Name ?? "<anonymous>",
            new ExecutionTypeDiagnosticsSnapshot(
                type.AssemblyQualifiedName ?? type.FullName ?? type.Name));
    }

    public static ExecutionDescriptorSnapshot CreateDescriptor(ExecutionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new ExecutionDescriptorSnapshot(
            ExecutionDiagnosticIdFactory.CreateOperation(descriptor.OperationKey),
            descriptor.Point,
            CreateType(descriptor.ComponentType),
            descriptor.ContractType is null ? null : CreateType(descriptor.ContractType),
            CreateMethod(descriptor.EntryMethod),
            CreateType(descriptor.InputType),
            CreateType(descriptor.ResultType),
            descriptor.IsBusinessOperation,
            descriptor.TransactionMode,
            new ExecutionOperationDiagnosticsSnapshot(descriptor.OperationKey));
    }

    private static ExecutionMethodSnapshot? CreateMethod(MethodInfo? method)
    {
        if (method is null)
        {
            return null;
        }

        var name = CreateMethodName(method);
        var declaringType = method.DeclaringType?.GetCleanFullName();
        var parameters = string.Join(", ", method.GetParameters().Select(FormatParameter));
        var signature = $"{name}({parameters}) → {method.ReturnType.GetCleanFullName()}";
        var displaySignature = string.IsNullOrWhiteSpace(declaringType)
            ? signature
            : $"{declaringType}.{signature}";
        var canonicalDeclaringType = method.DeclaringType?.AssemblyQualifiedName;
        var canonicalSignature = string.IsNullOrWhiteSpace(canonicalDeclaringType)
            ? method.ToString() ?? method.Name
            : $"{canonicalDeclaringType}.{method.ToString() ?? method.Name}";

        return new ExecutionMethodSnapshot(
            name,
            displaySignature,
            new ExecutionMethodDiagnosticsSnapshot(canonicalSignature));
    }

    private static string CreateMethodName(MethodInfo method)
    {
        if (!method.IsGenericMethod)
        {
            return method.Name;
        }

        var arguments = string.Join(", ", method.GetGenericArguments().Select(static type => type.GetCleanName()));
        return $"{method.Name}<{arguments}>";
    }

    private static string FormatParameter(ParameterInfo parameter)
    {
        var parameterType = parameter.ParameterType;
        var modifier = parameter.IsOut
            ? "out "
            : parameterType.IsByRef
                ? parameter.IsIn ? "in " : "ref "
                : string.Empty;
        var displayType = (parameterType.IsByRef ? parameterType.GetElementType()! : parameterType)
            .GetCleanFullName();
        return $"{modifier}{displayType}";
    }
}
