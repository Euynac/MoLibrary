using Microsoft.Extensions.Options;
using Monica.DevOps.K8S.Abstractions;
using Monica.DevOps.K8S.Models;

namespace Monica.DevOps.K8S.Services;

public class K8SRuntimeConfigStore(IOptions<Modules.ModuleK8SOption> options) : IK8SRuntimeConfigStore
{
    private readonly object _syncRoot = new();
    private K8SRuntimeConfig _current = options.Value.RuntimeConfig.Clone().Normalize();

    public K8SRuntimeConfig GetCurrent()
    {
        lock (_syncRoot)
        {
            return _current.Clone();
        }
    }

    public void Update(K8SRuntimeConfig runtimeConfig)
    {
        ArgumentNullException.ThrowIfNull(runtimeConfig);

        lock (_syncRoot)
        {
            _current = runtimeConfig.Clone();
        }
    }
}
