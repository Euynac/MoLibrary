using NPOI.SS.UserModel;

namespace Monica.Office.Excel.Npoi
{
    /// <summary>
    /// NPOI workbook handler interface
    /// </summary>
    public interface INpoiExcelHandle
    {
        /// <summary>
        /// Gets an <see cref="IWorkbook"/>.
        /// </summary>
        /// <param name="physicalPath">The physical file path.</param>
        /// <returns></returns>
        IWorkbook GetWorkbook(string physicalPath);

        /// <summary>
        /// Gets an <see cref="IWorkbook"/>.
        /// </summary>
        /// <param name="fileStream">The file stream.</param>
        /// <returns></returns>
        IWorkbook GetWorkbook(Stream fileStream);

        /// <summary>
        /// Gets merged-cell metadata for a cell.
        /// </summary>
        /// <param name="sheet">The worksheet.</param>
        /// <param name="cell">The cell.</param>
        /// <returns>MergedInfo</returns>
        ExcelCellMergedInfo GetCellMergedInfo(ISheet sheet, ICell? cell);

        /// <summary>
        /// Gets the cell value.
        /// </summary>
        /// <param name="cell">The cell.</param>
        /// <returns></returns>
        object? GetCellValue(ICell? cell);

        /// <summary>
        /// Gets the value of a formula cell.
        /// </summary>
        /// <param name="formulaValue"></param>
        /// <param name="cell"></param>
        /// <returns></returns>
        object? GetCellValue(CellValue? formulaValue, ICell? cell);

        /// <summary>
        /// Gets the value from the merged cell range that contains the cell.
        /// </summary>
        /// <param name="sheet">The worksheet.</param>
        /// <param name="cell">The cell.</param>
        /// <returns>Returns the first value in the merged range for a merged cell; otherwise returns the current cell value.</returns>
        object? GetMergedCellValue(ISheet sheet, ICell cell);

        /// <summary>
        /// Gets the default format for a cell value.
        /// </summary>
        /// <param name="workbook"></param>
        /// <param name="cellValue"></param>
        /// <returns></returns>
        short GetDefaultFormat(IWorkbook workbook,
            CellValueDefaultFormatEnum cellValue = CellValueDefaultFormatEnum.文本);

        /// <summary>
        /// Converts a cell value.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="columnIndex">The current column index, zero-based.</param>
        /// <param name="valueType">The target value type, for example <c>PropertyInfo.PropertyType</c>, <c>typeof(int?)</c>, <c>typeof(bool)</c>, or <c>typeof(string)</c>.</param>
        /// <returns></returns>
        object? ConverterCellValue(IRow? row, int columnIndex, Type valueType);

        /// <summary>
        /// Gets the default cell style.
        /// <para>An <c>.xls</c> workbook can define at most 4000 styles, so call this outside loops.</para>
        /// </summary>
        /// <param name="workbook">Workbook</param>
        /// <param name="style">CellStyleEnum</param>
        /// <returns></returns>
        ICellStyle GetDefaultCellStyle(IWorkbook workbook, ExcelCellStyleEnum style = ExcelCellStyleEnum.默认);

        /// <summary>
        /// Sets the width for a single column. Call this after creating the column. Auto-fit requires existing cell data.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="columnIndex">The column index, zero-based.</param>
        /// <param name="columnSize">The column width in characters, from 0 to 255.</param>
        /// <param name="columnAutoSize">Whether to auto-fit the column.</param>
        void SetColumnWidth(ISheet sheet, int columnIndex, int columnSize, bool columnAutoSize);

        /// <summary>
        /// Sets the default width for all columns. Call this immediately after creating the worksheet, before creating columns.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="columnSize">The column width in characters, from 0 to 255.</param>
        void SetColumnWidth(ISheet sheet, int columnSize);

        /// <summary>
        /// Sets the height for a single row. Call this after creating the row.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="row">The row.</param>
        /// <param name="rowHeight">The row height in points, from 0 to 409.</param>
        void SetRowHeight(ISheet sheet, IRow row, short rowHeight);

        /// <summary>
        /// Sets the default height for all rows. Call this immediately after creating the worksheet, before creating rows.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="rowHeight">The row height in points, from 0 to 409.</param>
        void SetRowHeight(ISheet sheet, short rowHeight);

        /// <summary>
        /// Writes the workbook to a stream and returns the bytes.
        /// </summary>
        /// <param name="workbook"></param>
        /// <returns></returns>
        byte[] GetAsByteArray(IWorkbook workbook);

        /// <summary>
        /// Merges a cell range.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="firstRow">The start row index, zero-based.</param>
        /// <param name="lastRow">The end row index, zero-based.</param>
        /// <param name="firstCol">The start column index, zero-based.</param>
        /// <param name="lastCol">The end column index, zero-based.</param>
        void MergedRegion(ISheet sheet, int firstRow, int lastRow, int firstCol, int lastCol);

        /// <summary>
        /// Gets the address string for a cell, for example <c>A1</c>.
        /// </summary>
        /// <param name="rowIndex">The row index, zero-based.</param>
        /// <param name="columnIndex">The column index, zero-based.</param>
        string GetCellAddress(int rowIndex, int columnIndex);

        /// <summary>
        /// Gets the address string for a cell, for example <c>A1</c>.
        /// </summary>
        /// <param name="cell">The cell.</param>
        string GetCellAddress(ICell cell);
    }
}
