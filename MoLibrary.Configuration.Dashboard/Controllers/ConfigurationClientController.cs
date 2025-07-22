using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MoLibrary.Configuration.Dashboard.Interfaces;
using MoLibrary.Configuration.Dashboard.Model;
using MoLibrary.Core.Module.ModuleController;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.Dashboard.Controllers;

/// <summary>
/// 配置客户端控制器，提供配置热更新API
/// </summary>
[ApiController]
[Route("api/option")]
public class ConfigurationClientController : MoModuleControllerBase
{
    private readonly IMoConfigurationModifier _modifier;
    private readonly ILogger<ConfigurationClientController> _logger;

    public ConfigurationClientController(
        IMoConfigurationModifier modifier,
        ILogger<ConfigurationClientController> logger)
    {
        _modifier = modifier;
        _logger = logger;
    }

    /// <summary>
    /// 配置中心更新指定配置
    /// </summary>
    /// <param name="request">更新请求</param>
    /// <returns>更新结果</returns>
    [HttpPost("update")]
    public async Task<IActionResult> UpdateConfig([FromBody] DtoUpdateConfig request)
    {
        try
        {
            var value = request.Value;

            if ((await _modifier.IsOptionExist(request.Key)).IsOk(out var option))
            {
                var result = await _modifier.UpdateOption(option, value);
                return Ok(result);
            }

            if ((await _modifier.IsConfigExist(request.Key)).IsOk(out var config))
            {
                var result = await _modifier.UpdateConfig(config, value);
                return Ok(result);
            }

            return BadRequest(Res.Fail($"更新失败，找不到Key为{request.Key}的配置"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新配置失败: {Key}", request.Key);
            return BadRequest(Res.Fail($"更新配置失败: {ex.Message}"));
        }
    }
}