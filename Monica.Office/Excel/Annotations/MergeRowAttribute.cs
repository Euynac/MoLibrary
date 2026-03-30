namespace Monica.Office.Excel.Annotations
{
    /// <summary>
    /// Merge Excel rows (for export only).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class MergeRowAttribute : Attribute
    {
    }
}
