using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module.BuilderWrapper;
using MoLibrary.Core.Module.Features;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.Interfaces;
public class MoModuleGuide
{
    /// <summary>
    /// 指示模块配置来源
    /// </summary>
    public EMoModules? GuideFrom { get; set; }
    
    /// <summary>
    /// Gets the target module enum this guide is for.
    /// </summary>
    /// <returns>The enum representation of the target module.</returns>
    public virtual EMoModules GetTargetModuleEnum()
    {
        // Default implementation returns a placeholder value
        // This should be overridden in specific module guides
        return default;
    }
}
public class MoModuleGuide<TModule, TModuleOption, TModuleGuideSelf> : MoModuleGuide, IMoModuleGuide
    where TModuleOption : MoModuleOption<TModule>, new()
    where TModuleGuideSelf : MoModuleGuide<TModule, TModuleOption, TModuleGuideSelf>, new()
    where TModule : MoModule<TModule, TModuleOption>, IMoModuleStaticInfo
{
    public override EMoModules GetTargetModuleEnum()
    {
        if(ModuleAnalyser.ModuleTypeToEnumMap.TryGetValue(typeof(TModule), out var moduleEnum))
        {
            return moduleEnum;
        }
        moduleEnum = TModule.GetModuleEnum();
        ModuleAnalyser.RegisterModuleMapping(typeof(TModule), moduleEnum);
        return moduleEnum;
    }

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

    public void RegisterInstantly()
    {
        if (WebApplicationBuilderExtensions.WebApplicationBuilderInstance == null)
        {
            throw new InvalidOperationException($"WebApplicationBuilderInstance is not initialized. Please use {nameof(WebApplicationBuilderExtensions.ConfigMoModule)} method to enable module instantly registration.");
        }
        MoModuleRegisterCentre.RegisterServices(WebApplicationBuilderExtensions.WebApplicationBuilderInstance);
    }


    #region 注册到注册中心

    /// <summary>
    /// 注册模块类型并获取其注册请求信息。
    /// </summary>
    /// <typeparam name="TModule">模块类型。</typeparam>
    /// <returns>模块的注册请求信息。</returns>
    private ModuleRequestInfo RegisterModule()
    {
        var moduleType = typeof(TModule);
        if (MoModuleRegisterCentre.ModuleRegisterContextDict.TryGetValue(moduleType, out var requestInfo)) return requestInfo;

        requestInfo = new ModuleRequestInfo();
        requestInfo.BindModuleOption<TModuleOption>();
        MoModuleRegisterCentre.AddModuleRegisterContext(moduleType, requestInfo);
        // 设置必须配置的方法键
        requestInfo.RequiredConfigMethodKeys = GetRequestedConfigMethodKeys().ToList();

        return requestInfo;
    }

    /// <summary>
    /// 注册模块并添加注册请求。
    /// </summary>
    /// <typeparam name="TModule">模块类型。</typeparam>
    /// <param name="request">注册请求。</param>
    public void RegisterModule(ModuleRegisterRequest request)
    {
        var actions = RegisterModule().RegisterRequests;
        actions.Add(request);
    }

    /// <summary>
    /// 添加模块配置操作。
    /// </summary>
    /// <typeparam name="TModule">模块类型。</typeparam>
    /// <typeparam name="TOption">模块配置类型。</typeparam>
    /// <param name="order">配置操作执行顺序。</param>
    /// <param name="optionAction">配置操作委托。</param>
    /// <param name="guideFrom">配置操作来源模块。</param>
    public void AddConfigureAction<TOption>(int order, Action<TOption> optionAction, EMoModules? guideFrom) where TOption : class, IMoModuleOptionBase, new()
    {
        var requestInfo = RegisterModule();

        requestInfo.AddConfigureAction(order, optionAction);

        requestInfo.RegisterRequests.Add(
            new ModuleRegisterRequest($"ConfigureOption_{typeof(TOption).Name}_{Guid.NewGuid()}")
            {
                ConfigureContext = context =>
                {
                    context.Services!.Configure(optionAction);
                },
                RequestMethod = EMoModuleConfigMethods.ConfigureServices,
                Order = guideFrom != EMoModules.Developer ? order - 1 : order, //来自模块级联注册的Option的优先级始终比用户Order低1
                RequestFrom = guideFrom
            });
    }


    #endregion




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

        var targetModule = new TModuleGuideSelf().GetTargetModuleEnum();

        RegisterModule();

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
        RegisterModule(request);
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
    public TModuleGuideSelf ConfigureOption<TOption>(Action<TOption>? optionAction, int order) where TOption : class, IMoModuleOptionBase, new()
    {
        if(optionAction == null) return (TModuleGuideSelf) this;

        RegisterModule();
        AddConfigureAction(order, optionAction, GuideFrom);
        return (TModuleGuideSelf) this;
    }
    public TModuleGuideSelf ConfigureExtraOption<TOption>(Action<TOption>? optionAction, EMoModuleOrder order = EMoModuleOrder.Normal) where TOption : class, IMoModuleExtraOption<TModule>, new()
    {
        return ConfigureOption(optionAction, (int) order);
    }

    #endregion

}