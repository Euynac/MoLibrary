using System.ComponentModel.DataAnnotations;

namespace Monica.Office.Excel.Models;

/// <summary>
/// Request for configuring exported columns dynamically.
/// </summary>
/// <param name="queryName"></param>
public class ExcelHeaderRequest(string queryName)
{
    /// <summary>
    /// Name used for lookup.
    /// </summary>
    public string QueryName { get; set; } = queryName;

    /// <summary>
    /// Custom column name.
    /// </summary>
    public string? CustomHeaderName { get; set; }

    /// <summary>
    /// Custom format.
    /// </summary>
    public string? DataFormat { get; set; }
    /// <summary>
    /// Whether the column width is adjusted automatically.
    /// </summary>
    public bool? ColumnAutoSize { get; set; }
    /// <summary>
    /// Column width
    /// <para>Unit: characters.</para>
    /// <para>Range: [0-255].</para>
    /// </summary>
    [Range(0, 255)]
    public int? ColumnSize { get; set; }
}
