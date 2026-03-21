namespace Monica.Office.Excel.Models
{
    /// <summary>
    /// Excel header cell information.
    /// </summary>
    public class ExcelHeaderCellInfo : ExcelHeaderCellInfo<ExcelHeaderCell>
    {
        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ExcelHeaderCellInfo(string sheetName, int sheetIndex)
        {
            SheetName = sheetName;
            SheetIndex = sheetIndex;
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ExcelHeaderCellInfo(string sheetName, int sheetIndex, List<ExcelHeaderCell> headerCells) : base(sheetName, sheetIndex, headerCells)
        {
        }
    }

    /// <summary>
    /// Excel header information.
    /// </summary>
    public class ExcelHeaderCellInfo<T> where T : ExcelHeaderCell
    {
        /// <summary>
        /// Worksheet name.
        /// </summary>
        public string SheetName { get; set; } = string.Empty;

        /// <summary>
        /// Worksheet index (zero-based).
        /// </summary>
        public int SheetIndex { get; set; }

        /// <summary>
        /// Collection of header cells.
        /// </summary>
        public List<T> HeaderCells { get; set; } = [];

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ExcelHeaderCellInfo()
        {
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ExcelHeaderCellInfo(string sheetName, int sheetIndex)
        {
            SheetName = sheetName;
            SheetIndex = sheetIndex;
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ExcelHeaderCellInfo(string sheetName, int sheetIndex, List<T> headerCells) : this(sheetName, sheetIndex)
        {
            HeaderCells = headerCells;
        }
    }

    /// <summary>
    /// Excel header cell.
    /// </summary>
    public class ExcelHeaderCell
    {
        /// <summary>
        /// Header cell name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Row index (uses the original index base).
        /// </summary>
        public int RowIndex { get; set; }

        /// <summary>
        /// Column index (uses the original index base).
        /// </summary>
        public int ColumnIndex { get; set; }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ExcelHeaderCell()
        {
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public ExcelHeaderCell(string name, int rowIndex, int columnIndex)
        {
            Name = name;
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
        }
    }
}
