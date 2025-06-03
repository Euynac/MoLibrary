namespace MoLibrary.Office.Excel.Attributes
{
    /// <summary>
    /// Excel 合并列（仅导出时用）
    /// </summary>
    /// <remarks>
    /// 构造
    /// </remarks>
    /// <param name="propertyNames">属性名称集合</param>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class MergeColumnAttribute(params string[] propertyNames) : Attribute
    {
        /// <summary>
        /// 属性名称集合
        /// </summary>
        public string[] PropertyNames { get; } = propertyNames;
    }
}
