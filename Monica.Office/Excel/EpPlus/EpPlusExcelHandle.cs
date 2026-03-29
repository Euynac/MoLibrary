using OfficeOpenXml;

namespace Monica.Office.Excel.EpPlus
{
    /// <summary>
    /// EpPlus workbook handler
    /// </summary>
    public class EpPlusExcelHandle : IEpPlusExcelHandle
    {
        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        public EpPlusExcelHandle()
        {
        }

        /// <summary>
        /// Gets an <see cref="ExcelWorkbook"/>.
        /// </summary>
        /// <param name="physicalPath">The physical file path.</param>
        /// <returns></returns>
        public virtual ExcelWorkbook GetWorkbook(string physicalPath)
        {
            ExcelHelper.ValidationExcel(physicalPath);

            using var stream = new FileStream(physicalPath, FileMode.Open, FileAccess.Read);
            return GetWorkbook(stream);
        }

        /// <summary>
        /// Gets an <see cref="ExcelWorkbook"/>.
        /// </summary>
        /// <param name="fileStream">The file stream.</param>
        /// <returns></returns>
        public virtual ExcelWorkbook GetWorkbook(Stream fileStream)
        {
            try
            {
                var excelPackage = new ExcelPackage(fileStream);

                return excelPackage.Workbook;
            }
            catch (Exception e)
            {
                throw new Exception($"获取 Workbook 出错：{e.Message}", e);
            }
        }

        /// <summary>
        /// Gets the value of a merged cell.
        /// </summary>
        /// <param name="sheet">The worksheet.</param>
        /// <param name="rowIndex">The current row index, one-based.</param>
        /// <param name="columnIndex">The current column index, one-based.</param>
        /// <returns></returns>
        public virtual object? GetMergedCellValue(ExcelWorksheet sheet, int rowIndex, int columnIndex)
        {
            var cell = sheet.Cells[rowIndex, columnIndex];
            return GetMergedCellValue(sheet, cell);
        }

        /// <summary>
        /// Gets the value of a merged cell.
        /// </summary>
        /// <param name="sheet">The worksheet.</param>
        /// <param name="cell">The cell.</param>
        /// <returns></returns>
        public virtual object? GetMergedCellValue(ExcelWorksheet sheet, ExcelRange cell)
        {
            var address = sheet.MergedCells[cell.Start.Row, cell.Start.Column];
            if (address != null)
            {
                var excelAddress = new ExcelAddress(address);
                cell = sheet.Cells[excelAddress.Start.Row, excelAddress.Start.Column];
            }
            return cell.Value;
        }

        /// <summary>
        /// Converts a cell value.
        /// </summary>
        /// <param name="sheet">The worksheet.</param>
        /// <param name="rowIndex">The current row index, one-based.</param>
        /// <param name="columnIndex">The current column index, one-based.</param>
        /// <param name="valueType">The target value type, for example <c>PropertyInfo.PropertyType</c>, <c>typeof(int?)</c>, <c>typeof(bool)</c>, or <c>typeof(string)</c>.</param>
        /// <returns></returns>
        public virtual object? ConverterCellValue(ExcelWorksheet sheet, int rowIndex, int columnIndex, Type valueType)
        {
            var cell = sheet.Cells[rowIndex, columnIndex];
            return ConverterCellValue(sheet, cell, valueType);
        }

        /// <summary>
        /// Converts a cell value.
        /// </summary>
        /// <param name="sheet">The worksheet.</param>
        /// <param name="cell">The cell.</param>
        /// <param name="valueType">The target value type, for example <c>PropertyInfo.PropertyType</c>, <c>typeof(int?)</c>, <c>typeof(bool)</c>, or <c>typeof(string)</c>.</param>
        /// <returns></returns>
        public virtual object? ConverterCellValue(ExcelWorksheet sheet, ExcelRange cell, Type valueType)
        {
            var cellValue = GetMergedCellValue(sheet, cell);
            return cellValue.ConvertExcelCellValue(valueType);
        }

        /// <summary>
        /// Sets the width for a single column. Call this after creating the column. Auto-fit requires existing cell data.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="columnIndex">The column index, one-based.</param>
        /// <param name="columnSize">The column width.</param>
        /// <param name="columnAutoSize">Whether to auto-fit the column.</param>
        public virtual void SetColumnWidth(ExcelWorksheet sheet, int columnIndex, int columnSize, bool columnAutoSize)
        {
            if (columnSize > 0)
            {
                // The column must already exist before setting the width.
                sheet.Column(columnIndex).Width = columnSize > 255 ? 255 : columnSize;
            }
            else if (columnAutoSize)
            {
                // Auto-fit requires existing cell data in the column.
                sheet.Column(columnIndex).AutoFit();
            }
        }

        /// <summary>
        /// Sets the default width for all columns. Call this immediately after creating the worksheet, before creating columns.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="columnSize">The column width in characters, from 0 to 255.</param>
        public virtual void SetColumnWidth(ExcelWorksheet sheet, int columnSize)
        {
            if (columnSize > 0)
            {
                // Set this after creating the worksheet and before creating columns.
                sheet.DefaultColWidth = columnSize > 255 ? 255 : columnSize;
            }
        }

        /// <summary>
        /// Sets the height for a single row. Call this after creating the row.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="rowIndex">The row index, one-based.</param>
        /// <param name="rowHeight">The row height in points, from 0 to 409.</param>
        public virtual void SetRowHeight(ExcelWorksheet sheet, int rowIndex, short rowHeight)
        {
            if (rowHeight > 0)
            {
                // The row must already exist before setting the height.
                sheet.Row(rowIndex).Height = rowHeight > 409 ? 409 : rowHeight;
            }
        }

        /// <summary>
        /// Sets the default height for all rows. Call this immediately after creating the worksheet, before creating rows.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="rowHeight">The row height in points, from 0 to 409.</param>
        public virtual void SetRowHeight(ExcelWorksheet sheet, short rowHeight)
        {
            if (rowHeight > 0)
            {
                // Set this immediately after creating the worksheet and before creating rows.
                sheet.DefaultRowHeight = rowHeight > 409 ? 409 : rowHeight;
            }
        }

        /// <summary>
        /// Writes the workbook to a byte array.
        /// </summary>
        /// <param name="workbook"></param>
        /// <param name="sheet"></param>
        /// <returns></returns>
        public virtual byte[] GetAsByteArray(ExcelWorkbook workbook, ExcelWorksheet sheet)
        {
            using var excelPackage = new ExcelPackage();
            excelPackage.Workbook.Worksheets.Add(sheet.Name, sheet);

            return excelPackage.GetAsByteArray();
        }

        /// <summary>
        /// Merges a cell range.
        /// </summary>
        /// <param name="sheet"></param>
        /// <param name="fromRow">The start row index, one-based.</param>
        /// <param name="toRow">The end row index, one-based.</param>
        /// <param name="fromColumn">The start column index, one-based.</param>
        /// <param name="toColumn">The end column index, one-based.</param>
        public virtual void MergedRegion(ExcelWorksheet sheet, int fromRow, int toRow, int fromColumn, int toColumn)
        {
            sheet.Cells[fromRow, fromColumn, toRow, toColumn].Merge = true;
        }

        /// <summary>
        /// Gets the address string for a range, for example <c>A1</c>, <c>B1:C2</c>, <c>A:A</c>, <c>1:1</c>, or <c>A1:E2,G3:G5</c>.
        /// </summary>
        /// <param name="fromRow">The start row index, one-based.</param>
        /// <param name="toRow">The end row index, one-based.</param>
        /// <param name="fromColumn">The start column index, one-based.</param>
        /// <param name="toColumn">The end column index, one-based.</param>
        public virtual string GetCellAddress(int fromRow, int toRow, int fromColumn, int toColumn)
        {
            return new ExcelAddress(fromRow, fromColumn, toRow, toColumn).Address;
        }

        /// <summary>
        /// Gets the address string for a cell, for example <c>A1</c>.
        /// </summary>
        /// <param name="row">The row index, one-based.</param>
        /// <param name="column">The column index, one-based.</param>
        public virtual string GetCellAddress(int row,int column)
        {
            return new ExcelAddress(row, column, row, column).Address;
        }
    }
}
