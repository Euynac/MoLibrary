using OfficeOpenXml;

namespace Monica.Office.Excel.Providers.EpPlus
{
    /// <summary>
    /// EpPlus workbook support.
    /// </summary>
    internal interface IEpPlusWorkbookSupport
    {
        /// <summary>
        /// Gets an <see cref="ExcelWorkbook"/>.
        /// </summary>
        /// <param name="physicalPath">The physical file path.</param>
        /// <returns></returns>
        ExcelWorkbook GetWorkbook(string physicalPath);

        /// <summary>
        /// Gets an <see cref="ExcelWorkbook"/>.
        /// </summary>
        /// <param name="fileStream">The file stream.</param>
        /// <returns></returns>
        ExcelWorkbook GetWorkbook(Stream fileStream);

        /// <summary>
        /// Gets the value of a merged cell.
        /// </summary>
        /// <param name="sheet">The worksheet.</param>
        /// <param name="row">The current row index, one-based.</param>
        /// <param name="column">The current column index, one-based.</param>
        /// <returns></returns>
        object? GetMergedCellValue(ExcelWorksheet sheet, int row, int column);

        /// <summary>
        /// Gets the value of a merged cell.
        /// </summary>
        /// <param name="sheet">The worksheet.</param>
        /// <param name="cell">The cell.</param>
        /// <returns></returns>
        object? GetMergedCellValue(ExcelWorksheet sheet, ExcelRange cell);

        /// <summary>
        /// Converts a cell value.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="row">The current row index, one-based.</param>
        /// <param name="column">The current column index, one-based.</param>
        /// <param name="valueType">The target value type, for example <c>PropertyInfo.PropertyType</c>, <c>typeof(int?)</c>, <c>typeof(bool)</c>, or <c>typeof(string)</c>.</param>
        /// <returns></returns>
        object? ConvertCellValue(ExcelWorksheet sheet, int row, int column, Type valueType);

        /// <summary>
        /// Converts a cell value.
        /// </summary>
        /// <param name="sheet">The worksheet.</param>
        /// <param name="cell">The cell.</param>
        /// <param name="valueType">The target value type, for example <c>PropertyInfo.PropertyType</c>, <c>typeof(int?)</c>, <c>typeof(bool)</c>, or <c>typeof(string)</c>.</param>
        /// <returns></returns>
        object? ConvertCellValue(ExcelWorksheet sheet, ExcelRange cell, Type valueType);

        /// <summary>
        /// Sets the width for a single column. Call this after creating the column. Auto-fit requires existing cell data.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="columnIndex">The column index, one-based.</param>
        /// <param name="columnSize">The column width.</param>
        /// <param name="columnAutoSize">Whether to auto-fit the column.</param>
        void SetColumnWidth(ExcelWorksheet sheet, int columnIndex, int columnSize, bool columnAutoSize);

        /// <summary>
        /// Sets the default width for all columns. Call this immediately after creating the worksheet, before creating columns.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="columnSize">The column width in characters, from 0 to 255.</param>
        void SetColumnWidth(ExcelWorksheet sheet, int columnSize);

        /// <summary>
        /// Sets the height for a single row. Call this after creating the row.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="rowIndex">The row index, one-based.</param>
        /// <param name="rowHeight">The row height in points, from 0 to 409.</param>
        void SetRowHeight(ExcelWorksheet sheet, int rowIndex, short rowHeight);

        /// <summary>
        /// Sets the default height for all rows. Call this immediately after creating the worksheet, before creating rows.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="rowHeight">The row height in points, from 0 to 409.</param>
        void SetRowHeight(ExcelWorksheet sheet, short rowHeight);

        /// <summary>
        /// Writes the workbook to a byte array.
        /// </summary>
        /// <param name="workbook"></param>
        /// <param name="sheet"></param>
        /// <returns></returns>
        byte[] GetAsByteArray(ExcelWorkbook workbook, ExcelWorksheet sheet);

        /// <summary>
        /// Merges a cell range.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="fromRow">The start row index, one-based.</param>
        /// <param name="toRow">The end row index, one-based.</param>
        /// <param name="fromColumn">The start column index, one-based.</param>
        /// <param name="toColumn">The end column index, one-based.</param>
        void MergedRegion(ExcelWorksheet sheet, int fromRow, int toRow, int fromColumn, int toColumn);

        /// <summary>
        /// Gets the address string for a range, for example <c>A1</c>, <c>B1:C2</c>, <c>A:A</c>, <c>1:1</c>, or <c>A1:E2,G3:G5</c>.
        /// </summary>
        /// <param name="fromRow">The start row index, one-based.</param>
        /// <param name="toRow">The end row index, one-based.</param>
        /// <param name="fromColumn">The start column index, one-based.</param>
        /// <param name="toColumn">The end column index, one-based.</param>
        string GetCellAddress(int fromRow, int toRow, int fromColumn, int toColumn);

        /// <summary>
        /// Gets the address string for a cell, for example <c>A1</c>.
        /// </summary>
        /// <param name="row">The row index, one-based.</param>
        /// <param name="column">The column index, one-based.</param>
        string GetCellAddress(int row, int column);
    }
}
