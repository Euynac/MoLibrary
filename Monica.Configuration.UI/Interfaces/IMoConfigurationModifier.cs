using System.Text.Json.Nodes;
using Monica.Configuration.Model;
using Monica.Configuration.UI.Model;
using Monica.Tool.Results;

namespace Monica.Configuration.UI.Interfaces;

public interface IMoConfigurationModifier
{
    #region 配置项接口

    /// <summary>
    /// Query whether the configuration item exists. If it exists, return the configuration item instance.
    /// </summary>
    /// <param name="key">Configuration item Option Key</param>
    Task<Res<OptionItem>> IsOptionExist(string key);

    /// <summary>
    /// Update configuration items
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    Task<Res<DtoUpdateConfigRes>> UpdateOption(string key, JsonNode? value);

    /// <summary>
    /// Update configuration items
    /// </summary>
    /// <param name="option"></param>
    /// <param name="value"></param>
    Task<Res<DtoUpdateConfigRes>> UpdateOption(OptionItem option, JsonNode? value);

    #endregion

    #region 配置类接口

    /// <summary>
    /// Query whether the configuration class exists. If it exists, return the configuration class instance.
    /// </summary>
    /// <param name="key">Configuration class Key</param>
    Task<Res<MoConfiguration>> IsConfigExist(string key);

    /// <summary>
    /// Update configuration class
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    Task<Res<DtoUpdateConfigRes>> UpdateConfig(string key, JsonNode? value);

    /// <summary>
    /// Update configuration class
    /// </summary>
    /// <param name="config"></param>
    /// <param name="value"></param>
    Task<Res<DtoUpdateConfigRes>> UpdateConfig(MoConfiguration config, JsonNode? value);

    #endregion
}