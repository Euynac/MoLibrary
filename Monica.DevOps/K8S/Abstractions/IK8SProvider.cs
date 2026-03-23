using Monica.DevOps.K8S.Models;

namespace Monica.DevOps.K8S.Abstractions;

public interface IK8SProvider
{
    Task<string> ExecuteKubectlAsync(K8SRuntimeConfig runtimeConfig, string kubectlArguments, CancellationToken cancellationToken = default);
}
