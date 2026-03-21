namespace Monica.Office.Excel.Models
{
    /// <summary>
    /// Exported Excel merged-region information.
    /// </summary>
    public class ExcelExportMergedRegionInfo
    {
        /// <summary>
        /// Start row (zero-based).
        /// </summary>
        public int FromRowIndex { get; set; }

        /// <summary>
        /// End row (zero-based).
        /// </summary>
        public int ToRowIndex { get; set; }

        /// <summary>
        /// Start column (zero-based).
        /// </summary>
        public int FromColumnIndex { get; set; }

        /// <summary>
        /// End column (zero-based).
        /// </summary>
        public int ToColumnIndex { get; set; }

        /// <summary>
        /// Collection of property names.
        /// </summary>
        public string[] PropertyNames { get; set; }

        /// <summary>
        /// Value.
        /// </summary>
        public object? Value { get; set; }

        #region Methods

        /// <summary>
        /// Checks whether the value matches.
        /// </summary>
        /// <returns></returns>
        public bool IsValueEqual(object? value)
        {
            if (string.IsNullOrWhiteSpace(value?.ToString()) || string.IsNullOrWhiteSpace(Value?.ToString()))
            {
                return false;
            }
            return Value.Equals(value);
        }

        /// <summary>
        /// Whether rows can be merged.
        /// </summary>
        /// <returns></returns>
        public bool IsCanMergedRow()
        {
            return FromRowIndex != ToRowIndex && FromColumnIndex == ToColumnIndex;
        }

        /// <summary>
        /// Whether columns can be merged.
        /// </summary>
        /// <returns></returns>
        public bool IsCanMergedColumn()
        {
            return FromColumnIndex != ToColumnIndex && FromRowIndex == ToRowIndex;
        }

        /// <summary>
        /// Whether the row is within the range.
        /// </summary>
        /// <returns></returns>
        public bool IsInRangeRow(int rowIndex)
        {
            return rowIndex >= FromRowIndex && rowIndex <= ToRowIndex;
        }

        /// <summary>
        /// Whether the column is within the range.
        /// </summary>
        /// <returns></returns>
        public bool IsInRangeColumn(int columnIndex)
        {
            return columnIndex >= FromColumnIndex && columnIndex <= ToColumnIndex;
        }

        /// <summary>
        /// Whether the row and column are within the range.
        /// </summary>
        /// <returns></returns>
        public bool IsInRange(int rowIndex, int columnIndex)
        {
            return IsInRangeRow(rowIndex) && IsInRangeColumn(columnIndex);
        }

        /// <summary>
        /// Whether the row is before the start row.
        /// </summary>
        /// <returns></returns>
        public bool IsOutRangeRowFrom(int rowIndex)
        {
            return rowIndex < FromRowIndex;
        }

        /// <summary>
        /// Whether the row is after the end row.
        /// </summary>
        /// <returns></returns>
        public bool IsOutRangeRowTo(int rowIndex)
        {
            return rowIndex > ToRowIndex;
        }

        /// <summary>
        /// Whether the column is before the start column.
        /// </summary>
        /// <returns></returns>
        public bool IsOutRangeColumnFrom(int columnIndex)
        {
            return columnIndex < FromColumnIndex;
        }

        /// <summary>
        /// Whether the column is after the end column.
        /// </summary>
        /// <returns></returns>
        public bool IsOutRangeColumnTo(int columnIndex)
        {
            return columnIndex > ToColumnIndex;
        }

        /// <summary>
        /// Whether the row is adjacent to either boundary row.
        /// </summary>
        /// <returns></returns>
        public bool IsSiblingRow(int rowIndex)
        {
            return Math.Abs(rowIndex - ToRowIndex) == 1 || Math.Abs(rowIndex - FromRowIndex) == 1;
        }

        /// <summary>
        /// Whether the column is adjacent to either boundary column.
        /// </summary>
        /// <returns></returns>
        public bool IsSiblingColumn(int columnIndex)
        {
            return Math.Abs(columnIndex - ToColumnIndex) == 1 || Math.Abs(columnIndex - FromColumnIndex) == 1;
        }

        /// <summary>
        /// Whether it is the same row.
        /// </summary>
        /// <returns></returns>
        public bool IsSameRow(int rowIndex)
        {
            return rowIndex == FromRowIndex && rowIndex == ToRowIndex;
        }

        /// <summary>
        /// Whether it is the same column.
        /// </summary>
        /// <returns></returns>
        public bool IsSameColumn(int columnIndex)
        {
            return columnIndex == FromColumnIndex && columnIndex == ToColumnIndex;
        }

        #endregion

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ExcelExportMergedRegionInfo()
        {
            PropertyNames = [];
        }

    }
}
