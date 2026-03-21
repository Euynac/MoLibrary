namespace Monica.Office.Excel.Attributes
{
    /// <summary>
    /// Excel row height attribute (for export only, default: 20)
    /// <para>1. Apply to classes.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class RowHeightAttribute : Attribute
    {
        /// <summary>
        /// Header row height
        /// <para>Unit: points.</para>
        /// <para>Range: [0-409].</para>
        /// </summary>
        public short HeaderRowHeight { get; set; } = 20;

        /// <summary>
        /// Data row height
        /// <para>Unit: points.</para>
        /// <para>Range: [0-409].</para>
        /// </summary>
        public short DataRowHeight { get; set; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public RowHeightAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="rowHeight">Shared row height for both header and data rows.</param>
        public RowHeightAttribute(short rowHeight)
        {
            HeaderRowHeight = rowHeight;
            DataRowHeight = rowHeight;
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public RowHeightAttribute(short headerRowHeight, short dataRowHeight)
        {
            HeaderRowHeight = headerRowHeight;
            DataRowHeight = dataRowHeight;
        }
    }
}
