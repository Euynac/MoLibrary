using System.ComponentModel.DataAnnotations;

namespace MoLibrary.DomainDrivenDesign.AutoCrud.Interfaces;

/// <summary>
/// 请求含分页需求
/// </summary>
public interface IHasRequestPage : IHasRequestSkipCount
{
    /// <summary>
    /// 当前页数
    /// </summary>
    public int? Page { get; set; }

    /// <summary>
    /// 取消分页，获取所有数据
    /// </summary>
    public bool? DisablePage { get; set; }
}


/// <summary>
/// 请求含限制最大返回条数
/// </summary>
public interface IHasRequestLimitedResult
{
    /// <summary>
    /// 每次响应的最大条数。在分页中就是每页条数。
    /// </summary>
    [Range(1, 2147483647)]
    public int MaxResultCount { get; set; }
}

/// <summary>
/// 请求含跳过条数数
/// </summary>
public interface IHasRequestSkipCount : IHasRequestLimitedResult
{
    /// <summary>
    /// 从第一页第一条开始跳过的数目
    /// </summary>
    [Range(0, 2147483647)]
    public int? SkipCount { get; set; }
}