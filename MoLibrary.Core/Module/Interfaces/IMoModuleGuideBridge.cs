namespace MoLibrary.Core.Module.Interfaces;

public interface IMoModuleGuideBridge
{
    public void CheckRequiredMethod(string methodName, string? errorDetail = null);
}