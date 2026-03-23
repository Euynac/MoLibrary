using Monica.DevOps.K8S.Models;

namespace Monica.DevOps.K8S.Exceptions;

public enum K8SMessageCode
{
    ExecutionNodeRequired,
    SshUserNameRequired,
    SshPasswordRequired,
    NamespaceRequired,
    NamespaceOutsideScope,
    ServiceNotFound,
    ResourceNotFound,
    ResourceNamesRequired,
    SensitiveRestrictionMismatch,
    RemoteKubectlCommandFailed,
    NoRestartableTargetsRemain,
    NoRestartableTargetsFound,
    ServiceSelectorMissing,
    ServiceBackingWorkloadsMissing,
    KubectlJsonParseFailed,
    UnsupportedWorkloadKind
}

public sealed class K8SOperationException : Exception
{
    public K8SMessageCode MessageCode { get; }

    public IReadOnlyList<string> MessageArguments { get; }

    private K8SOperationException(
        K8SMessageCode messageCode,
        string message,
        IReadOnlyList<string> messageArguments,
        Exception? innerException = null)
        : base(message, innerException)
    {
        MessageCode = messageCode;
        MessageArguments = messageArguments;
    }

    public static K8SOperationException ExecutionNodeRequired()
    {
        return Create(K8SMessageCode.ExecutionNodeRequired);
    }

    public static K8SOperationException SshUserNameRequired()
    {
        return Create(K8SMessageCode.SshUserNameRequired);
    }

    public static K8SOperationException SshPasswordRequired()
    {
        return Create(K8SMessageCode.SshPasswordRequired);
    }

    public static K8SOperationException NamespaceRequired()
    {
        return Create(K8SMessageCode.NamespaceRequired);
    }

    public static K8SOperationException NamespaceOutsideScope(string namespaceName)
    {
        return Create(K8SMessageCode.NamespaceOutsideScope, namespaceName);
    }

    public static K8SOperationException ServiceNotFound(string serviceName, string namespaceName)
    {
        return Create(K8SMessageCode.ServiceNotFound, serviceName, namespaceName);
    }

    public static K8SOperationException ResourceNotFound(K8SResourceType resourceType, string resourceName, string namespaceName)
    {
        return Create(K8SMessageCode.ResourceNotFound, resourceType.ToDisplayName(), resourceName, namespaceName);
    }

    public static K8SOperationException ResourceNamesRequired()
    {
        return Create(K8SMessageCode.ResourceNamesRequired);
    }

    public static K8SOperationException SensitiveRestrictionMismatch(IEnumerable<string> restrictionKeywords)
    {
        ArgumentNullException.ThrowIfNull(restrictionKeywords);
        return Create(K8SMessageCode.SensitiveRestrictionMismatch, string.Join(", ", restrictionKeywords));
    }

    public static K8SOperationException RemoteKubectlCommandFailed(int exitStatus, string detail)
    {
        return Create(K8SMessageCode.RemoteKubectlCommandFailed, exitStatus.ToString(), detail);
    }

    public static K8SOperationException NoRestartableTargetsRemain()
    {
        return Create(K8SMessageCode.NoRestartableTargetsRemain);
    }

    public static K8SOperationException NoRestartableTargetsFound()
    {
        return Create(K8SMessageCode.NoRestartableTargetsFound);
    }

    public static K8SOperationException ServiceSelectorMissing(string serviceName)
    {
        return Create(K8SMessageCode.ServiceSelectorMissing, serviceName);
    }

    public static K8SOperationException ServiceBackingWorkloadsMissing(string serviceName)
    {
        return Create(K8SMessageCode.ServiceBackingWorkloadsMissing, serviceName);
    }

    public static K8SOperationException KubectlJsonParseFailed(Exception innerException)
    {
        ArgumentNullException.ThrowIfNull(innerException);
        return Create(K8SMessageCode.KubectlJsonParseFailed, [], innerException);
    }

    public static K8SOperationException UnsupportedWorkloadKind(string workloadKind)
    {
        return Create(K8SMessageCode.UnsupportedWorkloadKind, workloadKind);
    }

    private static K8SOperationException Create(
        K8SMessageCode messageCode,
        params string[] messageArguments)
    {
        return Create(messageCode, messageArguments, null);
    }

    private static K8SOperationException Create(
        K8SMessageCode messageCode,
        string[] messageArguments,
        Exception? innerException)
    {
        return new K8SOperationException(
            messageCode,
            BuildMessage(messageCode, messageArguments),
            messageArguments,
            innerException);
    }

    private static string BuildMessage(K8SMessageCode messageCode, IReadOnlyList<string> messageArguments)
    {
        return messageCode switch
        {
            K8SMessageCode.ExecutionNodeRequired => "Execution node is required.",
            K8SMessageCode.SshUserNameRequired => "SSH user name is required.",
            K8SMessageCode.SshPasswordRequired => "SSH password is required before connecting to the cluster.",
            K8SMessageCode.NamespaceRequired => "Namespace is required.",
            K8SMessageCode.NamespaceOutsideScope => $"Namespace '{messageArguments[0]}' is outside the configured namespace scope.",
            K8SMessageCode.ServiceNotFound => $"Service '{messageArguments[0]}' was not found in namespace '{messageArguments[1]}'.",
            K8SMessageCode.ResourceNotFound => $"{messageArguments[0]} '{messageArguments[1]}' was not found in namespace '{messageArguments[2]}'.",
            K8SMessageCode.ResourceNamesRequired => "At least one resource name is required.",
            K8SMessageCode.SensitiveRestrictionMismatch => $"Name does not contain any configured sensitive-operation keyword. Allowed keywords: {messageArguments[0]}.",
            K8SMessageCode.RemoteKubectlCommandFailed => $"Remote kubectl command failed ({messageArguments[0]}): {messageArguments[1]}",
            K8SMessageCode.NoRestartableTargetsRemain => "No restartable targets remain.",
            K8SMessageCode.NoRestartableTargetsFound => "No restartable targets were found.",
            K8SMessageCode.ServiceSelectorMissing => $"Service '{messageArguments[0]}' has no selector and cannot be mapped to workloads.",
            K8SMessageCode.ServiceBackingWorkloadsMissing => $"No backing workloads were found for service '{messageArguments[0]}'.",
            K8SMessageCode.KubectlJsonParseFailed => "Failed to parse kubectl JSON output.",
            K8SMessageCode.UnsupportedWorkloadKind => $"Unsupported workload kind '{messageArguments[0]}'.",
            _ => throw new ArgumentOutOfRangeException(nameof(messageCode), messageCode, null)
        };
    }
}
