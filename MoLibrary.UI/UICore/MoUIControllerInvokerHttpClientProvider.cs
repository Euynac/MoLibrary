using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using MoLibrary.Core.GlobalJson.Interfaces;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.ModuleController;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.UI.UICore;

/// <summary>
/// 基于HttpClient的UI Controller调用器实现
/// </summary>
/// <typeparam name="TControllerOption">Controller选项类型</typeparam>
public class MoUIControllerInvokerHttpClientProvider<TControllerOption> : IMoUIControllerInvoker<TControllerOption>
    where TControllerOption : class, IMoModuleControllerOption
{
    private readonly HttpClient _httpClient;
    private readonly NavigationManager _navigationManager;
    private readonly TControllerOption _option;
    private readonly IGlobalJsonOption _globalJsonOption;

    /// <summary>
    /// 初始化Controller调用器
    /// </summary>
    /// <param name="option">Controller选项</param>
    /// <param name="httpClient">HTTP客户端</param>
    /// <param name="navigationManager">导航管理器</param>
    /// <param name="globalJsonOption">全局JSON选项</param>
    public MoUIControllerInvokerHttpClientProvider(
        IOptions<TControllerOption> option,
        HttpClient httpClient,
        NavigationManager navigationManager,
        IGlobalJsonOption globalJsonOption)
    {
        _option = option.Value;
        _httpClient = httpClient;
        _navigationManager = navigationManager;
        _globalJsonOption = globalJsonOption;
    }

    /// <summary>
    /// 执行GET请求
    /// </summary>
    /// <typeparam name="TController">Controller类型</typeparam>
    /// <typeparam name="TResponse">响应数据类型</typeparam>
    /// <param name="path">API路径</param>
    /// <returns>响应结果</returns>
    public async Task<Res<TResponse>?> GetAsync<TController, TResponse>(string path)
        where TController : MoModuleControllerBase
    {
        try
        {
            var url = BuildUrl<TController>(path);
            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return DeserializeResponse<TResponse>(content);
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 执行POST请求
    /// </summary>
    /// <typeparam name="TController">Controller类型</typeparam>
    /// <typeparam name="TRequest">请求数据类型</typeparam>
    /// <typeparam name="TResponse">响应数据类型</typeparam>
    /// <param name="path">API路径</param>
    /// <param name="request">请求数据</param>
    /// <returns>响应结果</returns>
    public async Task<Res<TResponse>?> PostAsync<TController, TRequest, TResponse>(string path, TRequest request)
        where TController : MoModuleControllerBase
    {
        try
        {
            var url = BuildUrl<TController>(path);
            var jsonContent = SerializeRequest(request);
            var response = await _httpClient.PostAsync(url, jsonContent);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return DeserializeResponse<TResponse>(content);
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 构建完整的URL地址
    /// </summary>
    /// <typeparam name="TController">Controller类型</typeparam>
    /// <param name="path">API路径</param>
    /// <returns>完整URL</returns>
    private string BuildUrl<TController>(string path) where TController : MoModuleControllerBase
    {
        var controllerRoute = _option.GetRoute<TController>(path);
        var baseUri = _navigationManager.BaseUri.TrimEnd('/');
        return $"{baseUri}/{controllerRoute.TrimStart('/')}";
    }

    /// <summary>
    /// 序列化请求数据
    /// </summary>
    /// <typeparam name="TRequest">请求数据类型</typeparam>
    /// <param name="request">请求数据</param>
    /// <returns>序列化后的HTTP内容</returns>
    private StringContent SerializeRequest<TRequest>(TRequest request)
    {
        var json = JsonSerializer.Serialize(request, _globalJsonOption.GlobalOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    /// <summary>
    /// 反序列化响应数据
    /// </summary>
    /// <typeparam name="TResponse">响应数据类型</typeparam>
    /// <param name="content">响应内容</param>
    /// <returns>反序列化后的响应结果</returns>
    private Res<TResponse>? DeserializeResponse<TResponse>(string content)
    {
        return JsonSerializer.Deserialize<Res<TResponse>>(content, _globalJsonOption.GlobalOptions);
    }
} 