namespace Monica.Office.Excel.Models.Internal
{
    /// <summary>
    /// Excel data row range indexes.
    /// </summary>
    internal class ExcelRowRange
    {
        /// <summary>
        /// Start index (zero-based).
        /// </summary>
        public int StartIndex { get; set; }
        /// <summary>
        /// End index (zero-based).
        /// </summary>
        public int EndIndex { get; set; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ExcelRowRange()
        {
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ExcelRowRange(int startIndex, int endIndex)
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
        }
    }
}
