namespace Monica.Core.Modularity.Abstractions;

/// <summary>
/// Marks a module's single primary startup-frozen option object.
/// </summary>
public interface IModuleOptions
{
}

/// <summary>
/// Internal host-context binding contract implemented by Monica module options.
/// </summary>
internal interface IModuleOptionsContext
{
    /// <summary>
    /// Binds the option instance to the host that owns it.
    /// </summary>
    /// <param name="application">The owning Monica application.</param>
    void Bind(MonicaApplication application);
}
