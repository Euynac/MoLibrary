using System.ComponentModel.DataAnnotations;

namespace Monica.Office.Excel.Models
{
    /// <summary>
    /// Excel worksheet output.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ExcelSheetImportResult<T> where T : class, new()
    {
        /// <summary>
        /// Worksheet name.
        /// </summary>
        public string SheetName { get; set; } = string.Empty;

        /// <summary>
        /// Worksheet index
        /// <para>One-based index.</para>
        /// </summary>
        public int SheetIndex { get; set; }

        /// <summary>
        /// Total row count.
        /// </summary>
        public int TotalCount => Rows.Count;

        /// <summary>
        /// Invalid row count.
        /// </summary>
        public int InvalidCount => Rows.Count(a => !a.IsValid);

        /// <summary>
        /// Valid row count.
        /// </summary>
        public int ValidCount => TotalCount - InvalidCount;

        /// <summary>
        /// Row collection.
        /// </summary>
        public List<ExcelImportRowResult<T>> Rows { get; set; } = [];
    }

    /// <summary>
    /// Imported row information.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ExcelImportRowResult<T> where T : class, new()
    {
        /// <summary>
        /// Worksheet name.
        /// </summary>
        public string SheetName { get; set; } = string.Empty;

        /// <summary>
        /// Worksheet index
        /// <para>One-based index.</para>
        /// </summary>
        public int SheetIndex { get; set; }

        /// <summary>
        /// Row data.
        /// </summary>
        public T Row { get; set; } = new();

        /// <summary>
        /// Row number
        /// <para>One-based index.</para>
        /// </summary>
        public int RowNum { get; set; }

        /// <summary>
        /// Whether the row is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Validation errors (available only when <see cref="IsValid"/> = false).
        /// </summary>
        public List<ValidationResult> Errors { get; set; } = [];
    }
}
