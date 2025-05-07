using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.Interfaces;

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
    where TModuleOption : class, IMoModuleOption<TModule>, new()
    where TModuleGuideSelf : MoModuleGuide<TModule, TModuleOption, TModuleGuideSelf>, new()
    where TModule : MoModule<TModule, TModuleOption> 
{
    /// <summary>
    /// 指示模块必须进行的手动配置，若不配置，则会抛出异常提醒用户进行配置。
    /// </summary>
    /// <returns></returns>
    protected virtual string[] GetRequestedConfigMethodKeys()
    {
        return Array.Empty<string>();
    }

    /// <summary>
    /// 发出模块注册请求
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    public TModuleGuideSelf Register(Action<TModuleOption>? config = null)
    {
        if (config != null)
        {
            ConfigureOption(config);
        }

        MoModuleRegisterCentre.RegisterModule<TModule, TModuleOption>();
        return new TModuleGuideSelf();
    }
    
    protected void ConfigureExtraServices(string key, Action<ModuleRegisterContext<TModuleOption>> context, int order)
    {
        var request = new ModuleRegisterRequest(key) { Order = order };
        request.SetConfigureContext(context);
        MoModuleRegisterCentre.RegisterModule<TModule, TModuleOption>(request);
    }

    protected void ConfigureExtraServices(string key, Action<ModuleRegisterContext<TModuleOption>> context, EMoModuleOrder order = EMoModuleOrder.Normal) 
    {
        ConfigureExtraServices(key, context, (int)order);
    }

    public TModuleGuideSelf ConfigureOption<TOption>(Action<TOption> optionAction, int order) where TOption : class, IMoModuleOption, new()
    {

        MoModuleRegisterCentre.RegisterModule<TModule, TModuleOption>();
        MoModuleRegisterCentre.AddConfigureAction<TModule, TOption>(order, optionAction, GuideFrom);
        return (TModuleGuideSelf) this;
    }

    public TModuleGuideSelf ConfigureOption<TOption>(Action<TOption> optionAction, EMoModuleOrder order = EMoModuleOrder.Normal) where TOption : class, IMoModuleOption<TModule>, new()
    {
        return ConfigureOption(optionAction, (int)order);
    }
}