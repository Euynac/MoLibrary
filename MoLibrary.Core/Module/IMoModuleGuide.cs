using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Core.Module;

public enum EMoModuleOrder
{
    Normal = 0,
    PostConfig = 100,
    PreConfig = -100
}

public interface IMoModuleGuide
{

}

public class MoModuleGuide
{
    /// <summary>
    /// 指示模块配置来源
    /// </summary>
    public EMoModules? GuideFrom { get; set; }
}

public class MoModuleGuide<TModule, TModuleOption, TModuleGuideSelf> : MoModuleGuide, IMoModuleGuide
    where TModuleOption : IMoModuleOption<TModule>, new()
    where TModuleGuideSelf : MoModuleGuide<TModule, TModuleOption, TModuleGuideSelf>, new()
    where TModule : MoModule<TModule, TModuleOption> 
{
    protected virtual string[] GetRequestedConfigMethodKeys()
    {
        return Array.Empty<string>();
    }
    public TModuleGuideSelf Register(Action<TModuleOption>? config = null)
    {
        return new TModuleGuideSelf();
    }
    private static void ConfigureExtraServices(string key, Action<ModuleRegisterContext<TModuleOption>> context, int order, bool onlyOnce)
    {
        var moduleType = typeof(TModule);
        if (!MoModuleRegisterCentre.ModuleRegisterContextDict.TryGetValue(moduleType, out var extraActions))
        {
            extraActions = new List<ModuleRegisterRequest>();
            MoModuleRegisterCentre.ModuleRegisterContextDict[moduleType] = extraActions;
        }
        
        var request = new ModuleRegisterRequest(key) { Order = order, OnlyConfigOnce = onlyOnce};
        request.SetConfigureContext(context);
        extraActions.Add(request);
    }
    
    protected void ConfigureExtraServices(string key, Action<ModuleRegisterContext<TModuleOption>> context, int order)
    {
        ConfigureExtraServices(key, context, order, false);
    }

    protected void ConfigureExtraServices(string key, Action<ModuleRegisterContext<TModuleOption>> context, EMoModuleOrder order = EMoModuleOrder.Normal) 
    {
        ConfigureExtraServices(key, (Action<ModuleRegisterContext>)context, (int)order);
    }

    protected void ConfigureExtraServicesOnce(string key, Action<ModuleRegisterContext<TModuleOption>> context, EMoModuleOrder order = EMoModuleOrder.Normal)
    {
        ConfigureExtraServicesOnce(key, (Action<ModuleRegisterContext>)context, (int)order);
    }

    protected void ConfigureExtraServicesOnce(string key, Action<ModuleRegisterContext<TModuleOption>> context, int order)
    {
        ConfigureExtraServices(key, (Action<ModuleRegisterContext>)context, order, true);
    }

    private static TModuleGuideSelf ConfigureOption<TOption>(Action<TOption> extraOptionAction, int order, bool isExtraOption) where TOption : IMoModuleOption
    {
        throw new NotImplementedException();
    }

    public TModuleGuideSelf ConfigureExtraOption<TOption>(Action<TOption> extraOptionAction, EMoModuleOrder order = EMoModuleOrder.Normal) where TOption : IMoModuleExtraOption<TModule>
    {
        return ConfigureOption(extraOptionAction, (int)order, true);
    }

    public TModuleGuideSelf ConfigureOption<TOption>(Action<TOption> extraOptionAction, EMoModuleOrder order = EMoModuleOrder.Normal) where TOption : IMoModuleOption<TModule>
    {
        return ConfigureOption(extraOptionAction, (int)order, false);
    }
}