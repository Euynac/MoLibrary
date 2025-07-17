using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.ModuleController;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.UI.UICore;

/// <summary>
/// UI Controller调用器接口，用于调用Controller API
/// </summary>
/// <typeparam name="TControllerOption">Controller选项类型</typeparam>
public interface IMoUIControllerInvoker<TControllerOption>
    where TControllerOption : IMoModuleControllerOption
{
    /// <summary>
    /// 执行GET请求
    /// </summary>
    /// <typeparam name="TController">Controller类型</typeparam>
    /// <typeparam name="TResponse">响应数据类型</typeparam>
    /// <param name="path">API路径</param>
    /// <returns>响应结果</returns>
    Task<Res<TResponse>?> GetAsync<TController, TResponse>(string path)
        where TController : MoModuleControllerBase;

    /// <summary>
    /// 执行POST请求
    /// </summary>
    /// <typeparam name="TController">Controller类型</typeparam>
    /// <typeparam name="TRequest">请求数据类型</typeparam>
    /// <typeparam name="TResponse">响应数据类型</typeparam>
    /// <param name="path">API路径</param>
    /// <param name="request">请求数据</param>
    /// <returns>响应结果</returns>
    Task<Res<TResponse>?> PostAsync<TController, TRequest, TResponse>(string path, TRequest request)
        where TController : MoModuleControllerBase;
} 