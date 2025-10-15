using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.MoLogProvider;
using MoLibrary.Core.Module.BuilderWrapper;
using MoLibrary.Core.Module.Features;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Core.Module.Interfaces;
public class MoModuleGuide
{
    /// <summary>
    /// 指示模块配置来源
    /// </summary>
    public EMoModules? GuideFrom { get; set; }

    /// <summary>
    /// Lazy-loaded logger instance for this module guide.
    /// </summary>
    private readonly Lazy<ILogger> _loggerLazy;
    
    /// <summary>
    /// Gets the logger instance for this module guide.
    /// </summary>
    public ILogger Logger => _loggerLazy.Value;

    public MoModuleGuide()
    {
        _loggerLazy = new Lazy<ILogger>(() => LogProvider.For(GetType()));
    }

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

    /// <summary>
    /// Declares a dependency on another module from given module and returns a guide for configuring that module.
    /// </summary>
    /// <typeparam name="TDependsModuleGuide">Type of the module guide for the dependent module.</typeparam>
    /// <returns>A module guide for configuring the dependent module.</returns>
    internal static TDependsModuleGuide DeclareDependency<TDependsModuleGuide>(EMoModules fromModule, EMoModules? guideFrom)
        where TDependsModuleGuide : MoModuleGuide, new()
    {
        // Add dependency to the list if it's not already there
        var dependsOnEnum = new TDependsModuleGuide().GetTargetModuleEnum();

        // Register this dependency relationship in the ModuleAnalyser
        ModuleAnalyser.AddDependency(fromModule, dependsOnEnum);

        return new TDependsModuleGuide()
        {
            GuideFrom = guideFrom
        };
    }

    /// <summary>
    /// Declares a dependency on another module and returns a guide for configuring that module.
    /// </summary>
    /// <typeparam name="TOtherModuleGuide">Type of the module guide for the dependent module.</typeparam>
    /// <returns>A module guide for configuring the dependent module.</returns>
    public TOtherModuleGuide DependsOnModule<TOtherModuleGuide>()
        where TOtherModuleGuide : MoModuleGuide, new()
    {
        return DeclareDependency<TOtherModuleGuide>(GetTargetModuleEnum(), GuideFrom);
    }
}
public class MoModuleGuide<TModule, TModuleOption, TModuleGuideSelf> : MoModuleGuide, IMoModuleGuide, IMoModuleGuideBridge
    where TModuleOption : MoModuleOption<TModule>, new()
    where TModuleGuideSelf : MoModuleGuide<TModule, TModuleOption, TModuleGuideSelf>, new()
    where TModule : MoModule<TModule, TModuleOption, TModuleGuideSelf>, IMoModuleStaticInfo
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
    private ModuleRegisterInfo RegisterModule()
    {
        var moduleType = typeof(TModule);
        if (MoModuleRegisterCentre.TryGetModuleRequestInfo(moduleType, out var requestInfo)) return requestInfo;

        requestInfo = new ModuleRegisterInfo(moduleType);
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

    
    #endregion


    public void CheckRequiredMethod(string methodName, string? errorDetail = null)
    {
        //TODO 检查当前模块指定的方法是否已配置，否则抛出异常
        
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

        var targetModule = new TModuleGuideSelf().GetTargetModuleEnum();

        RegisterModule();

        return new TModuleGuideSelf();
    }


    #region 额外配置Module

    /// <summary>
    /// 配置模块的核心方法，用于注册模块配置请求
    /// </summary>
    /// <param name="key">配置方法的唯一标识符</param>
    /// <param name="secondKey">配置方法第二标志符，用于某些可多次调用该方法的情况，若本身可多次调用，可传入<see cref=" Guid.NewGuid()"/></param>
    /// <param name="context">模块注册上下文操作</param>
    /// <param name="order">配置执行顺序</param>
    /// <param name="requestMethod">请求的配置方法类型</param>
    private void ConfigureModule(string key, string? secondKey, Action<ModuleRegisterContext> context, int order,
        EMoModuleConfigMethods requestMethod)
    {
        var request = new ModuleRegisterRequest($"{key}{secondKey?.BeAfter("_")}")
        {
            Order = order,
            RequestFrom = GuideFrom, 
            RequestMethod = requestMethod,
            ConfigureContext = context, Key = key
        };
        RegisterModule(request);
    }

    /// <summary>
    /// 配置空注册，记录当前配置方法的调用。仅用于规避多次调用某些方法或未调用必须方法。
    /// </summary>
    /// <param name="key">配置方法的唯一标识符</param>
    protected internal void ConfigureEmpty([CallerMemberName] string key = "")
    {
        RegisterModule(new ModuleRegisterRequest(key));
    }

    /// <summary>
    /// 配置模块的服务注册
    /// </summary>
    /// <param name="context">服务配置上下文操作</param>
    /// <param name="order">配置执行的具体顺序值</param>
    /// <param name="secondKey">配置方法第二标志符，用于某些可多次调用该方法的情况，若本身可多次调用，可传入<see cref=" Guid.NewGuid()"/></param>
    /// <param name="key">配置方法的唯一标识符</param>
    protected internal void ConfigureServices(Action<ModuleRegisterContextWrapperForServices<TModuleOption>> context,
        int order, string? secondKey = null, [CallerMemberName] string key = "")
    {
        ConfigureModule(key, secondKey, registerContext =>
        {
            context.Invoke(new ModuleRegisterContextWrapperForServices<TModuleOption>(registerContext));
        }, order, EMoModuleConfigMethods.ConfigureServices);
    }

    /// <summary>
    /// <inheritdoc cref="ConfigureServices(System.Action{MoLibrary.Core.Module.Models.ModuleRegisterContextWrapperForServices{TModuleOption}},int,string?,string)"/>
    /// </summary>
    /// <param name="context">服务配置上下文操作</param>
    /// <param name="order">配置执行顺序枚举值</param>
    /// <param name="secondKey">配置方法第二标志符，用于某些可多次调用该方法的情况，若本身可多次调用，可传入<see cref=" Guid.NewGuid()"/></param>
    /// <param name="key">配置方法的唯一标识符</param>
    protected internal void ConfigureServices(Action<ModuleRegisterContextWrapperForServices<TModuleOption>> context,
        EMoModuleOrder order = EMoModuleOrder.Normal, string? secondKey = null, [CallerMemberName] string key = "")
    {
        ConfigureServices(context, (int)order, secondKey, key);
    }

    /// <summary>
    /// 配置应用程序构建器中间件管道
    /// </summary>
    /// <param name="context">应用程序构建器配置上下文操作</param>
    /// <param name="order">中间件执行的具体顺序值</param>
    /// <param name="secondKey">配置方法第二标志符，用于某些可多次调用该方法的情况，若本身可多次调用，可传入<see cref=" Guid.NewGuid()"/></param>
    /// <param name="key">配置方法的唯一标识符</param>
    protected internal void ConfigureApplicationBuilder(
        Action<ModuleRegisterContextWrapperForApplicationBuilder<TModuleOption>> context, int order,
        string? secondKey = null, [CallerMemberName] string key = "")
    {
        ConfigureModule(key, secondKey, registerContext =>
        {
            context.Invoke(new ModuleRegisterContextWrapperForApplicationBuilder<TModuleOption>(registerContext));
        }, order, EMoModuleConfigMethods.ConfigureApplicationBuilder);
    }

    /// <summary>
    /// <inheritdoc cref="ConfigureApplicationBuilder(System.Action{MoLibrary.Core.Module.Models.ModuleRegisterContextWrapperForApplicationBuilder{TModuleOption}},int,string?,string)"/>
    /// </summary>
    /// <param name="context">应用程序构建器配置上下文操作</param>
    /// <param name="order">中间件执行顺序枚举值</param>
    /// <param name="secondKey">配置方法第二标志符，用于某些可多次调用该方法的情况，若本身可多次调用，可传入<see cref=" Guid.NewGuid()"/></param>
    /// <param name="key">配置方法的唯一标识符</param>
    protected internal void ConfigureApplicationBuilder(
        Action<ModuleRegisterContextWrapperForApplicationBuilder<TModuleOption>> context,
        EMoModuleApplicationMiddlewaresOrder order, string? secondKey = null, [CallerMemberName] string key = "")
    {
        ConfigureApplicationBuilder(context, (int)order, secondKey, key);
    }

    /// <summary>
    /// 配置模块的后置服务设置
    /// </summary>
    /// <param name="context">后置服务配置上下文操作</param>
    /// <param name="order">配置执行的具体顺序值</param>
    /// <param name="secondKey">配置方法第二标志符，用于某些可多次调用该方法的情况，若本身可多次调用，可传入<see cref=" Guid.NewGuid()"/></param>
    /// <param name="key">配置方法的唯一标识符</param>
    protected internal void PostConfigureServices(
        Action<ModuleRegisterContextWrapperForServices<TModuleOption>> context,
        int order, string? secondKey = null, [CallerMemberName] string key = "")
    {
        ConfigureModule(key, secondKey, registerContext =>
        {
            context.Invoke(new ModuleRegisterContextWrapperForServices<TModuleOption>(registerContext));
        }, order, EMoModuleConfigMethods.PostConfigureServices);
    }

    /// <summary>
    /// <inheritdoc cref="PostConfigureServices(System.Action{MoLibrary.Core.Module.Models.ModuleRegisterContextWrapperForServices{TModuleOption}},int,string?,string)"/>
    /// </summary>
    /// <param name="context">后置服务配置上下文操作</param>
    /// <param name="order">配置执行顺序枚举值</param>
    /// <param name="secondKey">配置方法第二标志符，用于某些可多次调用该方法的情况，若本身可多次调用，可传入<see cref=" Guid.NewGuid()"/></param>
    /// <param name="key">配置方法的唯一标识符</param>
    protected internal void PostConfigureServices(
        Action<ModuleRegisterContextWrapperForServices<TModuleOption>> context,
        EMoModuleOrder order = EMoModuleOrder.Normal, string? secondKey = null, [CallerMemberName] string key = "")
    {
        PostConfigureServices(context, (int)order, secondKey, key);
    }

    /// <summary>
    /// 配置Web应用程序构建器
    /// </summary>
    /// <param name="context">Web应用程序构建器配置上下文操作</param>
    /// <param name="order">配置执行的具体顺序值</param>
    /// <param name="secondKey">配置方法第二标志符，用于某些可多次调用该方法的情况，若本身可多次调用，可传入<see cref=" Guid.NewGuid()"/></param>
    /// <param name="key">配置方法的唯一标识符</param>
    protected internal void ConfigureBuilder(Action<ModuleRegisterContextWrapperForBuilder<TModuleOption>> context,
        int order, string? secondKey = null, [CallerMemberName] string key = "")
    {
        ConfigureModule(key, secondKey, registerContext =>
        {
            context.Invoke(new ModuleRegisterContextWrapperForBuilder<TModuleOption>(registerContext));
        }, order, EMoModuleConfigMethods.ConfigureBuilder);
    }

    /// <summary>
    /// <inheritdoc cref="ConfigureBuilder(System.Action{MoLibrary.Core.Module.Models.ModuleRegisterContextWrapperForBuilder{TModuleOption}},int,string?,string)"/>
    /// </summary>
    /// <param name="context">Web应用程序构建器配置上下文操作</param>
    /// <param name="order">配置执行顺序枚举值</param>
    /// <param name="secondKey">配置方法第二标志符，用于某些可多次调用该方法的情况，若本身可多次调用，可传入<see cref=" Guid.NewGuid()"/></param>
    /// <param name="key">配置方法的唯一标识符</param>
    protected internal void ConfigureBuilder(Action<ModuleRegisterContextWrapperForBuilder<TModuleOption>> context,
        EMoModuleOrder order = EMoModuleOrder.Normal, string? secondKey = null, [CallerMemberName] string key = "")
    {
        ConfigureBuilder(context, (int)order, secondKey, key);
    }

    /// <summary>
    /// 配置模块的端点路由
    /// </summary>
    /// <param name="context">端点配置上下文操作</param>
    /// <param name="order">配置执行的具体顺序值</param>
    /// <param name="secondKey">配置方法第二标志符，用于某些可多次调用该方法的情况，若本身可多次调用，可传入<see cref=" Guid.NewGuid()"/></param>
    /// <param name="key">配置方法的唯一标识符</param>
    protected internal void ConfigureEndpoints(
        Action<ModuleRegisterContextWrapperForApplicationBuilder<TModuleOption>> context,
        int order, string? secondKey = null, [CallerMemberName] string key = "")
    {
        ConfigureModule(key, secondKey, registerContext =>
        {
            context.Invoke(new ModuleRegisterContextWrapperForApplicationBuilder<TModuleOption>(registerContext));
        }, order, EMoModuleConfigMethods.ConfigureEndpoints);
    }

    /// <summary>
    /// <inheritdoc cref="ConfigureEndpoints(System.Action{MoLibrary.Core.Module.Models.ModuleRegisterContextWrapperForApplicationBuilder{TModuleOption}},int,string?,string)"/>
    /// </summary>
    /// <param name="context">端点配置上下文操作</param>
    /// <param name="order">配置执行顺序枚举值</param>
    /// <param name="secondKey">配置方法第二标志符，用于某些可多次调用该方法的情况，若本身可多次调用，可传入<see cref=" Guid.NewGuid()"/></param>
    /// <param name="key">配置方法的唯一标识符</param>
    protected internal void ConfigureEndpoints(
        Action<ModuleRegisterContextWrapperForApplicationBuilder<TModuleOption>> context,
        EMoModuleOrder order = EMoModuleOrder.Normal, string? secondKey = null, [CallerMemberName] string key = "")
    {
        ConfigureEndpoints(context, (int)order, secondKey, key);
    }


    #endregion

    #region 额外设置

    /// <summary>
    /// 配置模块选项
    /// </summary>
    /// <param name="optionAction">模块选项配置操作</param>
    /// <param name="order">配置执行顺序枚举值</param>
    /// <param name="secondKey">配置方法第二标志符，用于某些可多次调用该方法的情况，若本身可多次调用，可传入<see cref=" Guid.NewGuid()"/></param>
    /// <param name="key">配置方法的唯一标识符</param>
    public TModuleGuideSelf ConfigureModuleOption(Action<TModuleOption>? optionAction,
        EMoModuleOrder order = EMoModuleOrder.Normal, string? secondKey = null, [CallerMemberName] string key = "")
    {
        return ConfigureOption(optionAction, (int) order, secondKey, key);
    }

    /// <summary>
    /// 配置模块选项
    /// </summary>
    /// <param name="optionAction">模块选项配置操作</param>
    /// <param name="order">配置执行顺序枚举值</param>
    /// <param name="secondKey">配置方法第二标志符，用于某些可多次调用该方法的情况，若本身可多次调用，可传入<see cref=" Guid.NewGuid()"/></param>
    /// <param name="key">配置方法的唯一标识符</param>
    private TModuleGuideSelf ConfigureOption<TOption>(Action<TOption>? optionAction, int order, string? secondKey,
        string key) where TOption : class, IMoModuleOptionBase, new()
    {
        if(optionAction == null) return (TModuleGuideSelf) this;

        var requestInfo = RegisterModule();
        requestInfo.AddConfigureAction(order, optionAction, GuideFrom, secondKey, key);
        return (TModuleGuideSelf) this;
    }

    /// <summary>
    /// 配置模块额外选项
    /// </summary>
    /// <param name="optionAction">模块额外选项配置操作</param>
    /// <param name="order">配置执行顺序枚举值</param>
    /// <param name="secondKey">配置方法第二标志符，用于某些可多次调用该方法的情况，若本身可多次调用，可传入<see cref=" Guid.NewGuid()"/></param>
    /// <param name="key">配置方法的唯一标识符</param>
    public TModuleGuideSelf ConfigureExtraOption<TOption>(Action<TOption>? optionAction,
        EMoModuleOrder order = EMoModuleOrder.Normal, string? secondKey = null, [CallerMemberName] string key = "") where TOption : class, IMoModuleExtraOption<TModule>, new()
    {
        return ConfigureOption(optionAction, (int) order, secondKey, key);
    }

    #endregion

}