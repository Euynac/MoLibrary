using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.GlobalJson.Interfaces;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.ModuleController;
using MoLibrary.Tool.MoResponse;
using MoLibrary.UI.UICore.Interfaces;

namespace MoLibrary.UI.UICore.Services;

/// <summary>
/// 基于HttpClient的UI Controller调用器实现
/// </summary>
/// <typeparam name="TControllerOption">Controller选项类型</typeparam>
/// <remarks>
/// 初始化Controller调用器
/// </remarks>
/// <param name="option">Controller选项</param>
/// <param name="httpClient">HTTP客户端</param>
/// <param name="navigationManager">导航管理器</param>
/// <param name="globalJsonOption">全局JSON选项</param>
public class UIControllerInvokerHttpClientProvider<TControllerOption>(
    IOptions<TControllerOption> option,
    HttpClient httpClient,
    NavigationManager navigationManager,
    IGlobalJsonOption globalJsonOption) : IUIControllerInvoker<TControllerOption>
    where TControllerOption : class, IMoModuleControllerOption
{
    private readonly TControllerOption _option = option.Value;

    /// <summary>
    /// 执行GET请求
    /// </summary>
    /// <typeparam name="TController">Controller类型</typeparam>
    /// <typeparam name="TResponse">响应数据类型</typeparam>
    /// <param name="path">API路径</param>
    /// <returns>响应结果</returns>
    public async Task<Res<TResponse>> GetAsync<TController, TResponse>(string path)
        where TController : MoModuleControllerBase
    {
        try
        {
            var url = BuildUrl<TController>(path);
            var response = await httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = DeserializeResponse<TResponse>(content);
                if (result == null)
                    return Res.Fail(ResponseCode.BadRequest, "反序列化响应失败，返回结果为空");
                
                return result;
            }
            
            return Res.Fail(ResponseCode.BadRequest, "HTTP请求失败，状态码：{0}", response.StatusCode);
        }
        catch (JsonException jsonException)
        {
            var message = jsonException.GetMessageRecursively();
            return Res.Fail(ResponseCode.BadRequest, "执行GET请求{0}失败，JSON反序列化错误：{1}", path, message);
        }
        catch (Exception e)
        {
            var message = e.GetMessageRecursively();
            return Res.Fail(ResponseCode.BadRequest, "执行GET请求{0}失败：{1}", path, message);
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
    public async Task<Res<TResponse>> PostAsync<TController, TRequest, TResponse>(string path, TRequest request)
        where TController : MoModuleControllerBase
    {
        try
        {
            var url = BuildUrl<TController>(path);
            var jsonContent = SerializeRequest(request);
            var response = await httpClient.PostAsync(url, jsonContent);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = DeserializeResponse<TResponse>(content);
                if (result == null)
                    return Res.Fail(ResponseCode.BadRequest, "反序列化响应失败，返回结果为空");
                
                return result;
            }
            
            return Res.Fail(ResponseCode.BadRequest, "HTTP请求失败，状态码：{0}", response.StatusCode);
        }
        catch (JsonException jsonException)
        {
            var message = jsonException.GetMessageRecursively();
            return Res.Fail(ResponseCode.BadRequest, "执行POST请求{0}失败，JSON反序列化错误：{1}", path, message);
        }
        catch (Exception e)
        {
            var message = e.GetMessageRecursively();
            return Res.Fail(ResponseCode.BadRequest, "执行POST请求{0}失败：{1}", path, message);
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
        var baseUri = navigationManager.BaseUri.TrimEnd('/');
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
        var json = JsonSerializer.Serialize(request, globalJsonOption.GlobalOptions);
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
        return JsonSerializer.Deserialize<Res<TResponse>>(content, globalJsonOption.GlobalOptions);
    }
} 