using System.Text.Json.Nodes;
using BuildingBlocksPlatform.Configuration.Dashboard.Model;
using BuildingBlocksPlatform.Configuration.Model;
using BuildingBlocksPlatform.SeedWork;
using MoLibrary.Tool.MoResponse;

namespace BuildingBlocksPlatform.Configuration.Dashboard.Interfaces;

public interface IMoConfigurationModifier
{
    #region 配置项接口

    /// <summary>
    /// 查询配置项是否存在，存在则返回配置项实例
    /// </summary>
    /// <param name="key">配置项Option Key</param>
    Task<Res<OptionItem>> IsOptionExist(string key);

    /// <summary>
    /// 更新配置项
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    Task<Res<DtoUpdateConfigRes>> UpdateOption(string key, JsonNode? value);

    /// <summary>
    /// 更新配置项
    /// </summary>
    /// <param name="option"></param>
    /// <param name="value"></param>
    Task<Res<DtoUpdateConfigRes>> UpdateOption(OptionItem option, JsonNode? value);

    #endregion

    #region 配置类接口

    /// <summary>
    /// 查询配置类是否存在，存在则返回配置类实例
    /// </summary>
    /// <param name="key">配置类Key</param>
    Task<Res<MoConfiguration>> IsConfigExist(string key);

    /// <summary>
    /// 更新配置类
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    Task<Res<DtoUpdateConfigRes>> UpdateConfig(string key, JsonNode? value);

    /// <summary>
    /// 更新配置类
    /// </summary>
    /// <param name="config"></param>
    /// <param name="value"></param>
    Task<Res<DtoUpdateConfigRes>> UpdateConfig(MoConfiguration config, JsonNode? value);

    #endregion
}