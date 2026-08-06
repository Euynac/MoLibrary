namespace Monica.Core.Modularity.Abstractions;

/// <summary>
/// Adds host-default-aware Minimal API settings to a Web module's primary options.
/// </summary>
/// <typeparam name="TModule">The Web module that owns these options.</typeparam>
public abstract class MinimalApiModuleOptions<TModule> : ModuleOptions<TModule>, IMinimalApiModuleOptions
    where TModule : IModule
{
    /// <summary>
    /// Sets the API group name.
    /// Keep it distinct from the Swagger tag because Swagger displays the tag by default unless a filter rewrites it.
    /// </summary>
    public string? ApiGroup { get; set; }

    /// <summary>
    /// Gets or sets whether this module exposes its Minimal API endpoints.
    /// When unset, the module follows <see cref="IMonicaModuleSystemOptions.EnableMinimalApiByDefault"/>.
    /// </summary>
    public bool? EnableMinimalApi { get; set; }

    /// <summary>
    /// Resolves the effective endpoint group name from the module value and host default.
    /// </summary>
    public string GetApiGroupName() => ApiGroup ?? ModuleSystem.DefaultApiGroupName ?? typeof(TModule).Name;

    /// <summary>
    /// Resolves whether the module should map its Minimal API endpoints.
    /// </summary>
    public bool GetIsMinimalApiEnabled() => EnableMinimalApi ?? ModuleSystem.EnableMinimalApiByDefault;
}
