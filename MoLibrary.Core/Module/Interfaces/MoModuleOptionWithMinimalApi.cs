namespace MoLibrary.Core.Module.Interfaces;

public class MoModuleOptionWithMinimalApi<TModule> : MoModuleOption<TModule>, IMoModuleOptionWithMinimalApi where TModule : IMoModule
{
    /// <summary>
    /// 设置该Api Group注意要与Swagger Tag区分，默认是显示Swagger Tag的。需要增加Filter自动转换。
    /// </summary>
    public string? ApiGroup { get; set; }
    public bool? IsMinimalApiDisabled { get; set; }
    public string GetApiGroupName() => ApiGroup ?? Mo.Options.DefaultModuleApiGroupName ?? typeof(TModule).Name;
    public bool GetIsMinimalApiDisabled() => IsMinimalApiDisabled ?? Mo.Options.DefaultMinimalApiDisabled ?? false;
}