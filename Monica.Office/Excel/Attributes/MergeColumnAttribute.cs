namespace Monica.Office.Excel.Attributes
{
    /// <summary>
    /// Merge Excel columns (for export only).
    /// </summary>
    /// <remarks>
    /// Initializes a new instance.
    /// </remarks>
    /// <param name="propertyNames">Collection of property names.</param>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class MergeColumnAttribute(params string[] propertyNames) : Attribute
    {
        /// <summary>
        /// Collection of property names.
        /// </summary>
        public string[] PropertyNames { get; } = propertyNames;
    }
}
