using Microsoft.AspNetCore.Mvc;
using MoLibrary.Configuration.Dashboard.Model;
using MoLibrary.Configuration.Dashboard.UIConfiguration.Services;
using MoLibrary.Core.Extensions;
using MoLibrary.Core.Module.ModuleController;

namespace MoLibrary.Configuration.Dashboard.Controllers;

/// <summary>
/// 配置客户端控制器，提供配置热更新API
/// </summary>
[ApiController]
[Route("api/option")]
public class ConfigurationClientController(ConfigurationClientService configurationClientService) : MoModuleControllerBase
{

    /// <summary>
    /// 配置中心更新指定配置
    /// </summary>
    /// <param name="request">更新请求</param>
    /// <returns>更新结果</returns>
    [HttpPost("update")]
    public async Task<IActionResult> UpdateConfig([FromBody] DtoUpdateConfig request)
    {
        var result = await configurationClientService.UpdateConfigAsync(request);
        return result.GetResponse(this);
    }
}