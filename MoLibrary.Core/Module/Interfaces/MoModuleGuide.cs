using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.Interfaces;
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
    /// <returns>必须配置的方法键数组</returns>
    protected virtual string[] GetRequestedConfigMethodKeys()
    {
        return Array.Empty<string>();
    }
    
    protected TOtherModuleGuide DependsOnModule<TOtherModuleGuide>()
        where TOtherModuleGuide : MoModuleGuide, new()
    {
        return new TOtherModuleGuide()
        {
            GuideFrom = GuideFrom
        };
    }
    
    /// <summary>
    /// 发出模块注册请求
    /// </summary>
    /// <param name="config">模块配置操作</param>
    /// <returns>模块引导实例</returns>
    public TModuleGuideSelf Register(Action<TModuleOption>? config = null)
    {
        if (config != null)
        {
            ConfigureModuleOption(config);
        }

        // 注册模块并获取ModuleRequestInfo
        var requestInfo = MoModuleRegisterCentre.RegisterModule<TModule, TModuleOption>();
        
        // 设置必须配置的方法键
        var requiredKeys = GetRequestedConfigMethodKeys();
        if (requiredKeys.Length > 0)
        {
            requestInfo.RequiredConfigMethodKeys.AddRange(requiredKeys);
        }
        
        return new TModuleGuideSelf();
    }


    #region 额外配置Module
    protected void ConfigureModule(string key, Action<ModuleRegisterContext> context, int order, EMoModuleConfigMethods requestMethod)
    {
        var request = new ModuleRegisterRequest(key)
        {
            Order = order, RequestFrom = GuideFrom, RequestMethod = requestMethod,
            ConfigureContext = context, Key = key
        };
        MoModuleRegisterCentre.RegisterModule<TModule, TModuleOption>(request);
    }

    protected void ConfigureServices(string key, Action<ModuleRegisterContextWrapperForServices<TModuleOption>> context, EMoModuleOrder order = EMoModuleOrder.Normal)
    {
        ConfigureModule(key, registerContext =>
        {
            context.Invoke(new ModuleRegisterContextWrapperForServices<TModuleOption>(registerContext));
        }, (int) order, EMoModuleConfigMethods.ConfigureServices);
    }

    /// <summary>
    /// Configures the application builder for the module
    /// </summary>
    /// <param name="key">Unique key for this configuration</param>
    /// <param name="context">Action to configure the application builder</param>
    /// <param name="order">Order in which this configuration should be applied</param>
    protected void ConfigureApplicationBuilder(string key, Action<ModuleRegisterContextWrapperForApplicationBuilder<TModuleOption>> context, EMoModuleOrder order = EMoModuleOrder.Normal)
    {
        ConfigureModule(key, registerContext =>
        {
            context.Invoke(new ModuleRegisterContextWrapperForApplicationBuilder<TModuleOption>(registerContext));
        }, (int) order, EMoModuleConfigMethods.ConfigureApplicationBuilder);
    }   

    /// <summary>
    /// Configures post-services setup for the module
    /// </summary>
    /// <param name="key">Unique key for this configuration</param>
    /// <param name="context">Action to configure post-services</param>
    /// <param name="order">Order in which this configuration should be applied</param>
    protected void PostConfigureServices(string key, Action<ModuleRegisterContextWrapperForServices<TModuleOption>> context, EMoModuleOrder order = EMoModuleOrder.Normal)
    {
        ConfigureModule(key, registerContext =>
        {
            context.Invoke(new ModuleRegisterContextWrapperForServices<TModuleOption>(registerContext));
        }, (int) order, EMoModuleConfigMethods.PostConfigureServices);
    }
    
    /// <summary>
    /// Configures the web application builder for the module
    /// </summary>
    /// <param name="key">Unique key for this configuration</param>
    /// <param name="context">Action to configure the web application builder</param>
    /// <param name="order">Order in which this configuration should be applied</param>
    protected void ConfigureBuilder(string key, Action<ModuleRegisterContextWrapperForBuilder<TModuleOption>> context, EMoModuleOrder order = EMoModuleOrder.Normal)
    {
        ConfigureModule(key, registerContext =>
        {
            context.Invoke(new ModuleRegisterContextWrapperForBuilder<TModuleOption>(registerContext));
        }, (int) order, EMoModuleConfigMethods.ConfigureBuilder);
    }

    protected void ConfigureEndpoints(string key, Action<ModuleRegisterContextWrapperForBuilder<TModuleOption>> context, EMoModuleOrder order = EMoModuleOrder.Normal)
    {
        ConfigureModule(key, registerContext =>
        {
            context.Invoke(new ModuleRegisterContextWrapperForBuilder<TModuleOption>(registerContext));
        }, (int) order, EMoModuleConfigMethods.ConfigureEndpoints);
    }


    #endregion

    #region 额外设置

    public TModuleGuideSelf ConfigureModuleOption(Action<TModuleOption>? optionAction, EMoModuleOrder order = EMoModuleOrder.Normal)
    {
        return ConfigureOption(optionAction, (int) order);
    }
    public TModuleGuideSelf ConfigureOption<TOption>(Action<TOption>? optionAction, int order) where TOption : class, IMoModuleOption, new()
    {
        if(optionAction == null) return (TModuleGuideSelf) this;

        MoModuleRegisterCentre.RegisterModule<TModule, TModuleOption>();
        MoModuleRegisterCentre.AddConfigureAction<TModule, TOption>(order, optionAction, GuideFrom);
        return (TModuleGuideSelf) this;
    }
    public TModuleGuideSelf ConfigureExtraOption<TOption>(Action<TOption>? optionAction, EMoModuleOrder order = EMoModuleOrder.Normal) where TOption : class, IMoModuleExtraOption<TModule>, new()
    {
        return ConfigureOption(optionAction, (int) order);
    }

    #endregion

}