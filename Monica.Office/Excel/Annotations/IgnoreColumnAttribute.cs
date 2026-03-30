namespace Monica.Office.Excel.Annotations
{
    /// <summary>
    /// Ignore this Excel column during import and export.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class IgnoreColumnAttribute : Attribute
    {
    }
}
