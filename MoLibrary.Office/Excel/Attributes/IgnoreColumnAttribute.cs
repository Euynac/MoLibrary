namespace MoLibrary.Office.Excel.Attributes
{
    /// <summary>
    /// Excel列字段导入/导出时忽略
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IgnoreColumnAttribute : Attribute
    {
    }
}
