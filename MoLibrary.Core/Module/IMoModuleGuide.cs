using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Core.Module;

public enum EMoModuleOrder
{
    Normal = 0,
    PostConfig = 100,
    PreConfig = -100
}

public interface IMoModuleGuide<TModule> where TModule : IMoModule
{

}

public class MoModuleGuide
{
    /// <summary>
    /// 指示模块配置来源
    /// </summary>
    public EMoModules? GuideFrom { get; set; }
    
}

public class MoModuleGuide<TModule> : MoModuleGuide, IMoModuleGuide<TModule> where TModule : IMoModule
{
    public virtual string[] GetRequestedConfigMethodKeys()
    {
        return Array.Empty<string>();
    }

    public void ConfigureExtraServices(string key, Action<IServiceCollection> configureServices, int order) 
    {
        var moduleType = typeof(TModule);
        if (!MoModuleRegisterCentre.ModuleRegisterContextDict.TryGetValue(moduleType, out var extraActions))
        {
            extraActions = new List<ModuleRegisterRequest>();
            MoModuleRegisterCentre.ModuleRegisterContextDict[moduleType] = extraActions;
        }
        extraActions.Add(new ModuleRegisterRequest(key) { ConfigureServices = configureServices, Order = order });
    }
    public void ConfigureExtraServices(string key, Action<IServiceCollection> configureServices, EMoModuleOrder order = EMoModuleOrder.Normal) 
    {
        ConfigureExtraServices(key, configureServices, (int)order);
    }

    public void ConfigureExtraServicesOnce(string key, Action<IServiceCollection> configureServices, EMoModuleOrder order = EMoModuleOrder.Normal)
    {
        ConfigureExtraServicesOnce(key, configureServices, (int)order);
    }

    public void ConfigureExtraServicesOnce(string key, Action<IServiceCollection> configureServices, int order)
    {
        var moduleType = typeof(TModule);
        if (!MoModuleRegisterCentre.ModuleRegisterContextDict.TryGetValue(moduleType, out var extraActions))
        {
            extraActions = new List<ModuleRegisterRequest>();
            MoModuleRegisterCentre.ModuleRegisterContextDict[moduleType] = extraActions;
        }

        //TODO 后续集中处理重复键
        if (extraActions.Any(x => x.Key == key))
        {
            return;
            //throw new InvalidOperationException($"ConfigureExtraServicesOnce for {moduleType} with key {key} already exists");
        }
        extraActions.Add(new ModuleRegisterRequest(key) { ConfigureServices = configureServices, RequestFrom = GuideFrom, Order = order });
    }
}