namespace Monica.Core.Modularity.Abstractions;

public class MinimalApiModuleOptions<TModule> : ModuleOptions<TModule>, IMinimalApiModuleOptions where TModule : IModule
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

    public string GetApiGroupName() => ApiGroup ?? ModuleSystem.DefaultApiGroupName ?? typeof(TModule).Name;

    public bool GetIsMinimalApiEnabled() => EnableMinimalApi ?? ModuleSystem.EnableMinimalApiByDefault;
}
