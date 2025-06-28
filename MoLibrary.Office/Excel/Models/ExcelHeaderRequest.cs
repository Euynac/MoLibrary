using System.ComponentModel.DataAnnotations;

namespace MoLibrary.Office.Excel.Models;

/// <summary>
/// 动态设置导出列请求
/// </summary>
/// <param name="queryName"></param>
public class ExcelHeaderRequest(string queryName)
{
    /// <summary>
    /// 用于查询的名字
    /// </summary>
    public string QueryName { get; set; } = queryName;

    /// <summary>
    /// 自定义列名
    /// </summary>
    public string? CustomHeaderName { get; set; }

    /// <summary>
    /// 自定义格式
    /// </summary>
    public string? DataFormat { get; set; }
    /// <summary>
    /// 列自动调整大小
    /// </summary>
    public bool? ColumnAutoSize { get; set; }
    /// <summary>
    /// 列宽
    /// <para>单位：字符</para>
    /// <para>取值区间： [0-255]</para>
    /// </summary>
    [Range(0, 255)]
    public int? ColumnSize { get; set; }
}