using System.Reflection;
using System.Text;

namespace Monica.Core.Execution;

internal static class ExecutionOperationKey
{
    private const string CONTRACT_VERSION = "monica-execution-operation/v1";

    public static string Create(
        ExecutionPoint point,
        Type componentType,
        Type? contractType,
        MethodInfo? entryMethod,
        Type inputType,
        Type resultType)
    {
        var builder = new StringBuilder(CONTRACT_VERSION);
        AppendSegment(builder, "point", point.Value);
        AppendSegment(builder, "component", FormatType(componentType));
        AppendSegment(builder, "contract", contractType is null ? string.Empty : FormatType(contractType));
        AppendSegment(builder, "method", entryMethod is null ? string.Empty : FormatMethod(entryMethod));
        AppendSegment(builder, "input", FormatType(inputType));
        AppendSegment(builder, "result", FormatType(resultType));
        return builder.ToString();
    }

    private static void AppendSegment(StringBuilder builder, string name, string value)
    {
        builder.Append('|')
            .Append(name)
            .Append(':')
            .Append(value.Length)
            .Append(':')
            .Append(value);
    }

    private static string FormatMethod(MethodInfo method)
    {
        var builder = new StringBuilder();
        if (method.DeclaringType is not null)
        {
            builder.Append(FormatType(method.DeclaringType)).Append('.');
        }

        builder.Append(method.Name);

        if (method.IsGenericMethod)
        {
            builder.Append("``").Append(method.GetGenericArguments().Length);
        }

        builder.Append('(');
        var parameters = method.GetParameters();
        for (var index = 0; index < parameters.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            builder.Append(FormatType(parameters[index].ParameterType));
        }

        return builder.Append(")->")
            .Append(FormatType(method.ReturnType))
            .ToString();
    }

    private static string FormatType(Type type)
    {
        if (type.IsByRef)
        {
            return $"{FormatType(type.GetElementType()!)}&";
        }

        if (type.IsPointer)
        {
            return $"{FormatType(type.GetElementType()!)}*";
        }

        if (type.IsArray)
        {
            return $"{FormatType(type.GetElementType()!)}[{new string(',', type.GetArrayRank() - 1)}]";
        }

        if (type.IsGenericParameter)
        {
            var prefix = type.DeclaringMethod is null ? "!" : "!!";
            return $"{prefix}{type.GenericParameterPosition}";
        }

        var assemblyIdentity = type.Assembly.FullName ?? type.Assembly.GetName().Name ?? "<anonymous>";
        if (!type.IsGenericType)
        {
            return $"{assemblyIdentity}:{type.FullName ?? type.Name}";
        }

        var genericDefinition = type.GetGenericTypeDefinition();
        var genericArguments = string.Join(',', type.GetGenericArguments().Select(FormatType));
        return $"{assemblyIdentity}:{genericDefinition.FullName ?? genericDefinition.Name}[{genericArguments}]";
    }
}
