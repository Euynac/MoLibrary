using System.Text;

namespace Monica.Core.Execution.Models.Internal;

internal static class ExecutionPipelinePlanKey
{
    private const string CONTRACT_VERSION = "monica-execution-plan/v1";

    public static string Create(ExecutionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        var builder = new StringBuilder(CONTRACT_VERSION);
        AppendSegment(builder, "operation", descriptor.OperationKey);
        AppendSegment(builder, "business", descriptor.IsBusinessOperation ? "true" : "false");
        AppendSegment(builder, "transaction", descriptor.TransactionMode.ToString());
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
}
