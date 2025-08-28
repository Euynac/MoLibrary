using System.Text.Json.Nodes;
using MoLibrary.Framework.Core.Model;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Framework.Services;

/// <summary>
/// 框架监控服务接口
/// </summary>
public interface IFrameworkMonitorService
{
    /// <summary>
    /// 获取所有项目单元信息
    /// </summary>
    /// <returns>项目单元列表</returns>
    Task<Res<List<DtoProjectUnit>>> GetAllProjectUnitsAsync();

    /// <summary>
    /// 获取领域事件信息
    /// </summary>
    /// <returns>领域事件信息列表</returns>
    Task<Res<List<DtoDomainEventInfo>>> GetDomainEventsAsync();

    /// <summary>
    /// 发布领域事件
    /// </summary>
    /// <param name="eventKey">事件键</param>
    /// <param name="eventContent">事件内容</param>
    /// <returns>发布结果</returns>
    Task<Res> PublishDomainEventAsync(string eventKey, JsonNode eventContent);

    /// <summary>
    /// 管理请求过滤
    /// </summary>
    /// <param name="urls">URL列表</param>
    /// <param name="disable">是否禁用</param>
    /// <returns>被禁用的URL列表</returns>
    Task<Res<List<string>>> ManageRequestFilterAsync(List<string>? urls, bool? disable);

    /// <summary>
    /// 获取枚举信息
    /// </summary>
    /// <param name="name">枚举名称</param>
    /// <returns>枚举信息</returns>
    Task<Res<List<DtoAssemblyEnumInfo>>> GetEnumInfoAsync(string? name = null);

    /// <summary>
    /// 通过键值获取项目单元
    /// </summary>
    /// <param name="key">项目单元键值</param>
    /// <returns>项目单元</returns>
    ProjectUnit? GetProjectUnitByKey(string key);
}