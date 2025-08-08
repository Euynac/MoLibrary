using Microsoft.Extensions.Logging;
using MoLibrary.Configuration.Dashboard.Interfaces;
using MoLibrary.Configuration.Dashboard.Model;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Configuration.Dashboard.UIConfiguration.Services;

/// <summary>
/// 配置客户端服务，提供配置热更新功能
/// </summary>
public class ConfigurationClientService(
    IMoConfigurationModifier modifier,
    ILogger<ConfigurationClientService> logger)
{
    /// <summary>
    /// 更新指定配置
    /// </summary>
    /// <param name="request">更新请求</param>
    /// <returns>更新结果</returns>
    public async Task<Res<DtoUpdateConfigRes>> UpdateConfigAsync(DtoUpdateConfig request)
    {
        try
        {
            var value = request.Value;

            if ((await modifier.IsOptionExist(request.Key)).IsOk(out var option))
            {
                return await modifier.UpdateOption(option, value);
            }

            if ((await modifier.IsConfigExist(request.Key)).IsOk(out var config))
            {
                return await modifier.UpdateConfig(config, value);
            }

            return Res.Fail($"更新失败，找不到Key为{request.Key}的配置");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新配置失败: {Key}", request.Key);
            return Res.Fail($"更新配置失败: {ex.Message}");
        }
    }
}