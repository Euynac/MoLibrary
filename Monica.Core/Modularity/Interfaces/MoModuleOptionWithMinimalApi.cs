namespace Monica.Core.Modularity.Interfaces;

public class MoModuleOptionWithMinimalApi<TModule> : MoModuleOption<TModule>, IMoModuleOptionWithMinimalApi where TModule : IMoModule
{
    /// <summary>
    /// Sets the API group name.
    /// Keep it distinct from the Swagger tag because Swagger displays the tag by default unless a filter rewrites it.
    /// </summary>
    public string? ApiGroup { get; set; }
    public bool? IsMinimalApiDisabled { get; set; }
    public string GetApiGroupName() => ApiGroup ?? Mo.Options.DefaultModuleApiGroupName ?? typeof(TModule).Name;
    public bool GetIsMinimalApiDisabled() => IsMinimalApiDisabled ?? Mo.Options.DefaultMinimalApiDisabled ?? false;
}
