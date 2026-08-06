namespace Monica.Core.Modularity.Models;

/// <summary>
/// Identifies the named ASP.NET Core routing boundary at which a module contributes middleware.
/// </summary>
public enum ModuleWebStage
{
    /// <summary>
    /// Runs before Monica calls <c>UseRouting()</c>.
    /// </summary>
    BeforeRouting,

    /// <summary>
    /// Runs after Monica calls <c>UseRouting()</c>.
    /// </summary>
    AfterRouting
}
