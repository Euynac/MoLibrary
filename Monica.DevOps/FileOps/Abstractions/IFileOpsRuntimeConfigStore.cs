using Monica.DevOps.FileOps.Models;

namespace Monica.DevOps.FileOps.Abstractions;

public interface IFileOpsRuntimeConfigStore
{
    FileOpsRuntimeConfig GetCurrent();

    void Update(FileOpsRuntimeConfig runtimeConfig);
}
