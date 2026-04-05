namespace Monica.Core.Modularity.Abstractions;

public interface IModuleRequirementChecker
{
    public void CheckRequiredMethod(string methodName, string? errorDetail = null);
}