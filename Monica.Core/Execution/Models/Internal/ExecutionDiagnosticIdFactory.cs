using System.Security.Cryptography;
using System.Text;

namespace Monica.Core.Execution.Models.Internal;

internal static class ExecutionDiagnosticIdFactory
{
    private const int ID_BYTE_LENGTH = 12;

    public static ExecutionOperationId CreateOperation(string canonicalKey) =>
        new(Create("op", canonicalKey));

    public static ExecutionPlanId CreatePlan(string canonicalKey) =>
        new(Create("plan", canonicalKey));

    private static string Create(string prefix, string canonicalKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalKey);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalKey));
        var compactHash = Convert.ToHexString(hash.AsSpan(0, ID_BYTE_LENGTH)).ToLowerInvariant();
        return $"{prefix}_{compactHash}";
    }
}
