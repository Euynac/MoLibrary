using Monica.DevOps.K8S.Models;

namespace Monica.DevOps.K8S.Abstractions;

public interface IK8SRuntimeConfigStore
{
    K8SRuntimeConfig GetCurrent();

    void Update(K8SRuntimeConfig runtimeConfig);
}
