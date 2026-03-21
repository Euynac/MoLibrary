namespace Monica.Office.Excel.Models
{
    /// <summary>
    /// Excel data row range indexes.
    /// </summary>
    public class ExcelDataRowRangeIndex
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
        public ExcelDataRowRangeIndex()
        {
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ExcelDataRowRangeIndex(int startIndex, int endIndex)
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
        }
    }
}
