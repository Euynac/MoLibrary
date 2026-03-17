namespace Monica.Core.Modularity.Interfaces;

public interface IMoModuleGuideBridge
{
    public void CheckRequiredMethod(string methodName, string? errorDetail = null);
}