using Microsoft.Extensions.Options;
using Monica.DevOps.FileOps.Abstractions;
using Monica.DevOps.FileOps.Models;

namespace Monica.DevOps.FileOps.Services;

public class FileOpsRuntimeConfigStore(IOptions<Modules.ModuleFileOpsOption> options) : IFileOpsRuntimeConfigStore
{
    private readonly object _syncRoot = new();
    private FileOpsRuntimeConfig _current = options.Value.RuntimeConfig.Clone().Normalize();

    public FileOpsRuntimeConfig GetCurrent()
    {
        lock (_syncRoot)
        {
            return _current.Clone();
        }
    }

    public void Update(FileOpsRuntimeConfig runtimeConfig)
    {
        ArgumentNullException.ThrowIfNull(runtimeConfig);

        lock (_syncRoot)
        {
            _current = runtimeConfig.Clone().Normalize();
        }
    }
}
