using Microsoft.Extensions.Localization;
using Monica.DevOps.K8S.Exceptions;
using Monica.DevOps.K8S.Localization;
using Monica.DevOps.K8S.Models;

namespace Monica.DevOps.K8S.Services.Support;

public class K8SMessageLocalizer(IStringLocalizer<K8SResource> localizer)
{
    public string GetRuntimeConfigSavedMessage(K8SRuntimeConfig runtimeConfig)
    {
        return runtimeConfig.NamespaceScope.Count == 0
            ? localizer["Messages.RuntimeConfigSavedAllNamespaces"].Value
            : localizer["Messages.RuntimeConfigSaved"].Value;
    }

    public string GetUnsupportedResourceTypeMessage(string resourceType)
    {
        return localizer["GeneratedMessages:UnsupportedResourceType", resourceType].Value;
    }

    public string TranslateExceptionMessage(Exception exception)
    {
        return exception is K8SOperationException operationException
            ? LocalizeGeneratedMessage(operationException.MessageCode, operationException.MessageArguments)
            : exception.Message;
    }

    public string LocalizeGeneratedMessage(K8SMessageCode messageCode, IReadOnlyList<string> arguments)
    {
        return messageCode switch
        {
            K8SMessageCode.ExecutionNodeRequired => localizer["GeneratedMessages:ExecutionNodeRequired"].Value,
            K8SMessageCode.SshUserNameRequired => localizer["GeneratedMessages:SshUserNameRequired"].Value,
            K8SMessageCode.SshPasswordRequired => localizer["GeneratedMessages:SshPasswordRequired"].Value,
            K8SMessageCode.NamespaceRequired => localizer["GeneratedMessages:NamespaceRequired"].Value,
            K8SMessageCode.NamespaceOutsideScope => localizer["GeneratedMessages:NamespaceOutsideScope", arguments[0]].Value,
            K8SMessageCode.ServiceNotFound => localizer["GeneratedMessages:ServiceNotFound", arguments[0], arguments[1]].Value,
            K8SMessageCode.ResourceNotFound => localizer["GeneratedMessages:ResourceNotFound", LocalizeResourceType(arguments[0]), arguments[1], arguments[2]].Value,
            K8SMessageCode.ResourceNamesRequired => localizer["GeneratedMessages:ResourceNamesRequired"].Value,
            K8SMessageCode.SensitiveRestrictionMismatch => localizer["GeneratedMessages:SensitiveRestrictionMismatch", arguments[0]].Value,
            K8SMessageCode.RemoteKubectlCommandFailed => localizer["GeneratedMessages:RemoteKubectlCommandFailed", arguments[0], GetRemoteKubectlCommandDetail(arguments[1])].Value,
            K8SMessageCode.NoRestartableTargetsRemain => localizer["GeneratedMessages:NoRestartableTargetsRemain"].Value,
            K8SMessageCode.NoRestartableTargetsFound => localizer["GeneratedMessages:NoRestartableTargetsFound"].Value,
            K8SMessageCode.ServiceSelectorMissing => localizer["GeneratedMessages:ServiceSelectorMissing", arguments[0]].Value,
            K8SMessageCode.ServiceBackingWorkloadsMissing => localizer["GeneratedMessages:ServiceBackingWorkloadsMissing", arguments[0]].Value,
            K8SMessageCode.KubectlJsonParseFailed => localizer["GeneratedMessages:KubectlJsonParseFailed"].Value,
            K8SMessageCode.UnsupportedWorkloadKind => localizer["GeneratedMessages:UnsupportedWorkloadKind", arguments[0]].Value,
            _ => throw new ArgumentOutOfRangeException(nameof(messageCode), messageCode, null)
        };
    }

    public string LocalizeResourceType(string resourceType)
    {
        return resourceType switch
        {
            "Service" => localizer["ResourceTypes.Service"].Value,
            "Deployment" => localizer["ResourceTypes.Deployment"].Value,
            "StatefulSet" => localizer["ResourceTypes.StatefulSet"].Value,
            "DaemonSet" => localizer["ResourceTypes.DaemonSet"].Value,
            _ => resourceType
        };
    }

    public K8SRestartPreview LocalizeRestartPreview(K8SRestartPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        return preview.LocalizeIgnoredTargets(LocalizeIgnoredTargetReason);
    }

    public K8SRestartResult LocalizeRestartResult(K8SRestartResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.LocalizeIgnoredTargets(LocalizeIgnoredTargetReason);
    }

    private string LocalizeIgnoredTargetReason(K8SRestartIgnoredTarget target)
    {
        return target.ReasonCode is { } messageCode
            ? LocalizeGeneratedMessage(messageCode, target.ReasonArguments)
            : target.Reason;
    }

    private string GetRemoteKubectlCommandDetail(string detail)
    {
        return string.IsNullOrWhiteSpace(detail)
            ? localizer["GeneratedMessages:NoCommandOutputReturned"].Value
            : detail;
    }
}
