namespace Monica.Core.Modularity.Abstractions;

/// <summary>
/// Defines the options for a module in the Monica.
/// </summary>
public interface IMinimalApiModuleOptions
{
    /// <summary>
    /// Gets the Swagger group name for the module.
    /// </summary>
    /// <returns></returns>
    public string GetApiGroupName();
    /// <summary>
    /// Gets a value indicating whether controllers are disabled for the module.
    /// </summary>
    /// <returns><c>true</c> if controllers are disabled; otherwise, <c>false</c>.</returns>
    public bool GetIsMinimalApiDisabled();
}
