using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Monica.Office.Excel.Annotations;
using Monica.Office.Excel.Models;
using Monica.Office.Excel.Models.Internal;
using Monica.Office.Excel.Services.Support;
using Monica.StateStore.TaskProgress.Models;
using Monica.Tool.Extensions;

namespace Monica.Office.Excel.Providers
{
    /// <summary>
    /// Base class for Excel export
    /// </summary>
    internal abstract class ExcelExportProviderBase<TWorkbook, TSheet, TRow, TCell, TCellStyle>
    {
        /// <summary>
        /// Initializes a new instance
        /// </summary>
        protected ExcelExportProviderBase()
        {
        }

        /// <summary>
        /// Exports data
        /// </summary>
        /// <typeparam name="TExportDto">The element type in <paramref name="data"/>. Header order follows property order.</typeparam>
        /// <param name="data">The data to export</param>
        /// <param name="optionAction">Configures export options</param>
        /// <param name="requests">The header names to export. If not specified, all headers are exported in <typeparamref name="TExportDto"/> property order. If specified, headers are exported in request order.</param>
        /// <param name="taskProgress">Optional task progress instance. No progress is reported when <see langword="null"/>.</param>
        /// <returns></returns>
        public byte[] Export<TExportDto>(IReadOnlyList<TExportDto> data, Action<ExcelExportOptions>? optionAction,
            ExcelHeaderRequest[] requests, TaskProgress? taskProgress = null)
            where TExportDto : class
        {
            try
            {
                taskProgress?.ThrowIfCancellationRequested();
                var options = new ExcelExportOptions();
                optionAction?.Invoke(options);
                options.CheckError();


                taskProgress?.IncrementAsync(1, "初始化Excel导出", "导出Excel").Wait();

                // Get the workbook
                var workbook = GetWorkbook(options);
                taskProgress?.IncrementAsync(4, "创建工作册").Wait();

                // Create the worksheet
                var worksheet = CreateSheet(workbook, options);

                taskProgress?.IncrementAsync(5, "创建工作表").Wait();

                // Validate the headers and get the export header metadata
                var headers = CheckHeader<TExportDto>(requests, options);

                taskProgress?.IncrementAsync(5, "验证表头").Wait();

                // Header row index
                var headerRowIndex = options.HeaderRowIndex - 1;

                // Create the styles and fonts for the selected header columns first
                var infoBundle = GetHeaderColumnStyleAndFont<TExportDto>(workbook, worksheet, headers);

                taskProgress?.IncrementAsync(10, "创建表头及数据样式").Wait();

                // Process header cells
                ProcessHeaderCell<TExportDto>(workbook, worksheet, headerRowIndex, infoBundle);
                taskProgress?.IncrementAsync(10, "处理表头").Wait();

                // Data row start index
                var dataRowIndex = options.DataRowStartIndex - 1;

                taskProgress?.IncrementAsync(5, "创建数据样式").Wait();

                // Process data cells
                ProcessDataCell(workbook, worksheet, data, dataRowIndex, infoBundle, out var footerRowIndex, taskProgress, 50);

                // Process footer statistics
                taskProgress?.IncrementAsync(5, "处理数据统计").Wait();
                ProcessFooterStatistics<TExportDto>(workbook, worksheet, dataRowIndex, footerRowIndex, infoBundle);


                // Process column widths. Auto-fit requires data, so this must run last.
                taskProgress?.IncrementAsync(5, "处理列宽").Wait();
                ProcessColumnWidth<TExportDto>(workbook, worksheet, headers);


                // Convert the workbook to bytes
                taskProgress?.IncrementAsync(5, "生成Excel文件").Wait();
                var result = GetAsByteArray(workbook, worksheet);
                taskProgress?.IncrementAsync(0, "Excel导出完成").Wait();

                return result;
            }
            catch (Exception e)
            {
                taskProgress?.CancelTaskAsync($"导出失败: {e.Message}").Wait();
                throw new Exception(e.Message, e);
            }
        }

        /// <summary>
        /// Validates the headers and gets the export header metadata
        /// </summary>
        /// <typeparam name="TExportDto"></typeparam>
        /// <param name="requests">The header names to export. If not specified, all headers are exported in <typeparamref name="TExportDto"/> property order. If specified, headers are exported in request order.</param>
        /// <param name="options"></param>
        public ExcelExportHeaderInfo[] CheckHeader<TExportDto>(ExcelHeaderRequest[] requests,
            ExcelExportOptions options) where TExportDto : class
        {
            return ExcelHeaderResolver.ResolveHeaders<TExportDto>(requests, options.DisallowDuplicateHeader);
        }

        #region Private

        /// <summary>
        /// Processes header cells
        /// </summary>
        /// <typeparam name="TExportDto"></typeparam>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="headerRowIndex">The header row index (zero-based)</param>
        /// <param name="infoBundle">The column style collection</param>
        private void ProcessHeaderCell<TExportDto>(TWorkbook workbook, TSheet worksheet, int headerRowIndex, List<ExcelExportHeaderInfoBundle<TCellStyle, HeaderStyleAttribute, HeaderFontAttribute, DataStyleAttribute, DataFontAttribute>> infoBundle) where TExportDto : class
        {
            // Process cell values, styles, fonts, and row height.
            for (var columnIndex = 0; columnIndex < infoBundle.Count; columnIndex++)
            {
                var info = infoBundle[columnIndex].Header;
                var headerStyle = infoBundle[columnIndex].HeaderStyle!;
                var p = info.PropertyInfo;

                // Create the cell
                var cell = CreateCell(workbook, worksheet, headerRowIndex, columnIndex);

                // Process the header cell value
                ProcessHeaderCellValue(workbook, worksheet, cell, p, info.HeaderName);

                // Process the header cell style and font
                SetHeaderCellStyleAndFont<TExportDto>(workbook, worksheet, cell, headerStyle);
            }

            // Process the header row height. The row must exist first.
            ProcessRowHeight<TExportDto>(workbook, worksheet, headerRowIndex, true);
        }

        /// <summary>
        /// Processes data cells
        /// </summary>
        /// <typeparam name="TExportDto">The element type in <paramref name="data"/></typeparam>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="data">The data collection</param>
        /// <param name="rowIndex">The next row index (zero-based)</param>
        /// <param name="infoBundle">The data style collection</param>
        /// <param name="nextRowIndex">The next row index (zero-based)</param>
        /// <param name="taskProgress">Optional task progress instance.</param>
        /// <param name="progressWeight">The weight of data processing in the overall progress. Only applies when <paramref name="taskProgress"/> is not <see langword="null"/>.</param>
        /// <returns>The next row index (zero-based)</returns>
        private void ProcessDataCell<TExportDto>(TWorkbook workbook, TSheet worksheet,
            IReadOnlyList<TExportDto> data, int rowIndex,
            List<ExcelExportHeaderInfoBundle<TCellStyle, HeaderStyleAttribute, HeaderFontAttribute, DataStyleAttribute,
                DataFontAttribute>> infoBundle,
            out int nextRowIndex, TaskProgress? taskProgress = null, int progressWeight = 0)
            where TExportDto : class
        {
            // Mergeable row regions
            var rowMergedList = new List<ExcelMergedRegion>();
            var rowMergedHeader = GetHeaderProperties<TExportDto>().Where(a => a.GetCustomAttribute<MergeRowAttribute>() != null).Select(a => a.Name).ToList();

            // Mergeable column regions
            var columnMergedList = new List<ExcelMergedRegion>();
            var columnMergedHeader = typeof(TExportDto).GetCustomAttributes<MergeColumnAttribute>().Select(a => a.PropertyNames.Distinct().ToArray()).Where(a => a.Length > 1).ToList();

            // Validate merge attribute settings
            CheckMergeAttribute<TExportDto>(columnMergedHeader);

            // Progress-related variables
            var dataCount = data.Count;
            var initialStep = taskProgress?.Status.CurrentStep;
            var progressIncrement = dataCount > 0 && taskProgress != null ? progressWeight / (double) dataCount : 0;

            taskProgress?.UpdatePhaseAsync("处理数据", $"开始处理数据，共 {dataCount} 条").Wait();

            // Process cell values, styles, fonts, and merged regions.
            var processedCount = 0;
            foreach (var d in data)
            {
                taskProgress?.ThrowIfCancellationRequested();

                for (var columnIndex = 0; columnIndex < infoBundle.Count; columnIndex++)
                {
                    var info = infoBundle[columnIndex].Header;
                    var dataStyle = infoBundle[columnIndex].DataStyle!;
                    var p = info.PropertyInfo;

                    // Create the cell
                    var cell = CreateCell(workbook, worksheet, rowIndex, columnIndex);

                    // Process the data cell value
                    var value = p.GetValue(d);
                    ProcessDataCellValue(workbook, worksheet, cell, p, value);

                    // Process the data cell style and font
                    SetDataCellStyleAndFont<TExportDto>(workbook, worksheet, cell, dataStyle);

                    // Process column merging
                    ProcessMergeColumn(columnMergedHeader, columnMergedList, rowIndex, columnIndex, p, value);

                    // Process row merging
                    ProcessMergeRow(rowMergedHeader, rowMergedList, rowIndex, columnIndex, p, value);
                }

                // Process the data row height. The row must exist first.
                ProcessRowHeight<TExportDto>(workbook, worksheet, rowIndex, false);

                // Next row index
                rowIndex++;

                // Update progress when a task progress instance is available.
                if (taskProgress != null)
                {
                    processedCount++;
                    if (processedCount % 50 == 0 || processedCount == dataCount) // Update after every 50 rows or when all rows are processed.
                    {
                        taskProgress.UpdateStatusAsync(initialStep!.Value + (int) (progressIncrement * processedCount), $"已处理 {processedCount}/{dataCount} 条数据").Wait();
                    }
                }
            }

            // Next row index
            nextRowIndex = rowIndex;

            // Remove regions that cannot be merged
            columnMergedList.RemoveAll(m => !m.IsCanMergedColumn());
            rowMergedList.RemoveAll(m => !m.IsCanMergedRow());

            // If a property is merged by column, remove its row merges. Column merging takes precedence.
            rowMergedList.RemoveAll(m => columnMergedList.Any(a => a.PropertyNames.Intersect(m.PropertyNames).Any()));

            // Merge the cell regions
            taskProgress?.IncrementAsync(0, "合并单元格").Wait();

            // All merged region metadata
            var mergedRegion = rowMergedList.Concat(columnMergedList).ToList();

            // Process merged regions
            foreach (var m in mergedRegion)
            {
                SetMergedRegion(workbook, worksheet, m.FromRowIndex, m.ToRowIndex, m.FromColumnIndex, m.ToColumnIndex);
            }

            taskProgress?.IncrementAsync(0, "数据处理完成").Wait();
        }

        /// <summary>
        /// Validates merge attribute settings
        /// </summary>
        /// <typeparam name="TExportDto">The element type in the collection</typeparam>
        /// <param name="columnMergedHeader">The column-merge header metadata</param>
        /// <returns>The next row index (zero-based)</returns>
        private void CheckMergeAttribute<TExportDto>(IReadOnlyList<string[]> columnMergedHeader) where TExportDto : class
        {
            var className = typeof(TExportDto).Name;
            var properties = GetHeaderProperties<TExportDto>();
            var attrName = nameof(MergeColumnAttribute);

            // Validate missing property names, duplicated properties across attributes, and inconsistent property types within the same attribute.
            for (var index = 0; index < columnMergedHeader.Count; index++)
            {
                var names = columnMergedHeader[index];
                var num = index + 1;

                // Missing property names
                var noExist = names.Where(a => properties.All(p => p.Name != a)).Select(a => a);
                if (noExist.Any())
                {
                    throw new Exception(
                        $"类【{className}】的第 {num} 个 {attrName} 指定的属性名称未找到：{string.Join(",", noExist)}");
                }

                // Inconsistent property types within the same attribute
                var type = names.Select(n => properties.First(b => n == b.Name).PropertyType);
                if (type.Distinct().Count() > 1)
                {
                    throw new Exception(
                        $"类【{className}】的第 {num} 个 {attrName} 指定的属性类型不一致");
                }
            }

            // A single property appears in multiple attributes
            var duplicate = columnMergedHeader.SelectMany(a => a).GroupBy(a => a)
                .Where(a => a.Count() > 1)
                .Select(a => a.Key).ToList();
            if (duplicate.Any())
            {
                throw new Exception(
                    $"类【{className}】中多个合并列的属性重复：{string.Join(",", duplicate)}");
            }
        }

        /// <summary>
        /// Processes row merging
        /// </summary>
        /// <param name="rowMergedHeader">The row-merge headers</param>
        /// <param name="mergedList">The merged region metadata collection</param>
        /// <param name="rowIndex">The current row index (zero-based)</param>
        /// <param name="columnIndex">The current column index (zero-based)</param>
        /// <param name="propertyInfo">The property metadata</param>
        /// <param name="value">The value</param>
        private void ProcessMergeRow(IReadOnlyList<string> rowMergedHeader, ICollection<ExcelMergedRegion> mergedList, int rowIndex, int columnIndex, PropertyInfo propertyInfo, object? value)
        {
            if (rowMergedHeader.All(a => a != propertyInfo.Name))
            {
                return;
            }

            // Get the last merged region for the current column
            var merge = mergedList.LastOrDefault(a => a.PropertyNames.Contains(propertyInfo.Name));

            // Whether the values are equal
            var isValueEqual = merge?.IsValueEqual(value) == true;

            // Create a new merged region when none exists, the column differs, the value changed after a mergeable range,
            // or the value matches but the row is not adjacent.
            if (merge == null || !merge.IsSameColumn(columnIndex) || !isValueEqual && merge.IsCanMergedRow() || isValueEqual && !merge.IsSiblingRow(rowIndex))
            {
                mergedList.Add(new ExcelMergedRegion
                {
                    PropertyNames = [propertyInfo.Name],
                    Value = value,
                    FromRowIndex = rowIndex,
                    ToRowIndex = rowIndex,
                    FromColumnIndex = columnIndex,
                    ToColumnIndex = columnIndex
                });
            }
            else if (!isValueEqual) // Reset the region when the value changes.
            {
                merge.Value = value;
                merge.FromRowIndex = rowIndex;
                merge.ToRowIndex = rowIndex;
                merge.FromColumnIndex = columnIndex;
                merge.ToColumnIndex = columnIndex;
            }
            else // Extend the merged region when the value matches on an adjacent row.
            {
                if (merge.IsOutRangeRowFrom(rowIndex))
                {
                    merge.FromRowIndex -= 1;
                }
                else if (merge.IsOutRangeRowTo(rowIndex))
                {
                    merge.ToRowIndex += 1;
                }
            }
        }

        /// <summary>
        /// Processes column merging
        /// </summary>
        /// <param name="columnMergedHeader">The column-merge headers</param>
        /// <param name="mergedList">The merged region metadata collection</param>
        /// <param name="rowIndex">The current row index (zero-based)</param>
        /// <param name="columnIndex">The current column index (zero-based)</param>
        /// <param name="propertyInfo">The property metadata</param>
        /// <param name="value">The value</param>
        private void ProcessMergeColumn(IReadOnlyList<string[]>? columnMergedHeader, ICollection<ExcelMergedRegion> mergedList, int rowIndex, int columnIndex, PropertyInfo propertyInfo, object? value)
        {
            // Process column merging
            if (columnMergedHeader == null || !columnMergedHeader.Any(a => a.Contains(propertyInfo.Name)))
            {
                return;
            }

            // Get the last merged region for the current column
            var merge = mergedList.LastOrDefault(a => a.PropertyNames.Contains(propertyInfo.Name));

            // Whether the values are equal
            var isValueEqual = merge?.IsValueEqual(value) == true;

            // Create a new merged region when none exists, the row differs, the value changed after a mergeable range,
            // or the value matches but the column is not adjacent.
            if (merge == null || !merge.IsSameRow(rowIndex) || !isValueEqual && merge.IsCanMergedColumn() || isValueEqual && !merge.IsSiblingColumn(columnIndex))
            {
                mergedList.Add(new ExcelMergedRegion
                {
                    PropertyNames = columnMergedHeader.First(a => a.Contains(propertyInfo.Name)),
                    Value = value,
                    FromRowIndex = rowIndex,
                    ToRowIndex = rowIndex,
                    FromColumnIndex = columnIndex,
                    ToColumnIndex = columnIndex
                });
            }
            else if (!isValueEqual) // Reset the region when the value changes.
            {
                merge.Value = value;
                merge.FromRowIndex = rowIndex;
                merge.ToRowIndex = rowIndex;
                merge.FromColumnIndex = columnIndex;
                merge.ToColumnIndex = columnIndex;
            }
            else  // Extend the merged region when the value matches in an adjacent column.
            {
                if (merge.IsOutRangeColumnFrom(columnIndex))
                {
                    merge.FromColumnIndex -= 1;
                }
                else if (merge.IsOutRangeColumnTo(columnIndex))
                {
                    merge.ToColumnIndex += 1;
                }
            }
        }

        /// <summary>
        /// Processes column widths. Columns must exist first, and auto-fit requires data.
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="headers">The export header metadata</param>

        private void ProcessColumnWidth<TExportDto>(TWorkbook workbook, TSheet worksheet, ExcelExportHeaderInfo[] headers) where TExportDto : class
        {
            for (var i = 0; i < headers.Length; i++)
            {
                var info = headers[i];
                var columnIndex = i;
                var p = info.PropertyInfo;

                // Set the column width
                var styleAttr = p.GetHeaderStyleAttr<TExportDto>();
                SetColumnWidth(workbook, worksheet, columnIndex, styleAttr.ColumnSize, styleAttr.ColumnAutoSize);
            }
        }

        /// <summary>
        /// Processes row height. The row must exist first.
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="rowIndex">The row index</param>
        /// <param name="isHeader">Whether the row is a header row</param>

        private void ProcessRowHeight<TExportDto>(TWorkbook workbook, TSheet worksheet, int rowIndex, bool isHeader) where TExportDto : class
        {
            var attr = typeof(TExportDto).GetCustomAttribute<RowHeightAttribute>() ?? new RowHeightAttribute();

            SetRowHeight(workbook, worksheet, rowIndex, isHeader ? attr.HeaderRowHeight : attr.DataRowHeight);
        }

        /// <summary>
        /// Processes the header cell value
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="cell">The cell</param>
        /// <param name="propertyInfo">The property currently being processed</param>
        /// <param name="value">The property value</param>
        private void ProcessHeaderCellValue(TWorkbook workbook, TSheet worksheet, TCell cell, PropertyInfo propertyInfo, object value)
        {
            try
            {
                // Set the cell value
                SetCellValue(workbook, worksheet, cell, typeof(string), value);

            }
            catch (Exception e)
            {
                throw new Exception($"【{propertyInfo.Name}】的表头值设置出错：{e.Message}", e);
            }

        }

        /// <summary>
        /// Processes the data cell value
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="cell">The cell</param>
        /// <param name="propertyInfo">The property currently being processed</param>
        /// <param name="value">The property value</param>
        private void ProcessDataCellValue(TWorkbook workbook, TSheet worksheet, TCell cell, PropertyInfo propertyInfo, object? value)
        {
            try
            {
                if (value == null)
                {
                    var defaultValue = propertyInfo.GetCustomAttribute<DefaultValueAttribute>();
                    if (defaultValue != null)
                    {
                        value = defaultValue.Value;
                    }
                }

                if (value != null)
                {
                    // Set the cell value
                    SetCellValue(workbook, worksheet, cell, propertyInfo.PropertyType, value);
                }

            }
            catch (Exception e)
            {
                throw new Exception($"【{propertyInfo.Name}】的数据值设置出错：{e.Message}", e);
            }

        }

        /// <summary>
        /// Gets the styles and fonts for all exported header columns and their data cells
        /// </summary>
        /// <typeparam name="TExportDto"></typeparam>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="headers"></param>
        private List<ExcelExportHeaderInfoBundle<TCellStyle, HeaderStyleAttribute, HeaderFontAttribute, DataStyleAttribute, DataFontAttribute>> GetHeaderColumnStyleAndFont<TExportDto>(TWorkbook workbook,
                TSheet worksheet, ExcelExportHeaderInfo[] headers) where TExportDto : class
        {
            var styles = new List<ExcelExportHeaderInfoBundle<TCellStyle, HeaderStyleAttribute, HeaderFontAttribute, DataStyleAttribute, DataFontAttribute>>();

            // Default header style
            var headerDefaultAttr = typeof(TExportDto).GetHeaderStyleFont<TExportDto>();
            var headerDefaultStyle =
                CreateHeaderStyleAndFont<TExportDto>(workbook, worksheet, headerDefaultAttr.StyleAttr, headerDefaultAttr.FontAttr);

            // Default data style
            var dataDefaultAttr = typeof(TExportDto).GetDataStyleFont<TExportDto>();
            var dataDefaultStyle = CreateDataStyleAndFont<TExportDto>(workbook, worksheet, dataDefaultAttr.StyleAttr,
                dataDefaultAttr.FontAttr);

            foreach (var info in headers)
            {
                var propertyInfo = info.PropertyInfo;
              
                // Add the combined header/data style bundle
                styles.Add(new ExcelExportHeaderInfoBundle<TCellStyle, HeaderStyleAttribute, HeaderFontAttribute, DataStyleAttribute, DataFontAttribute>(info)
                {
                    HeaderStyle = CreateHeaderStyle(propertyInfo, info),
                    DataStyle = CreateCellStyle(propertyInfo, info)
                });

            }

            return styles;
            ExcelCellStyleOutput<TCellStyle, HeaderStyleAttribute, HeaderFontAttribute> CreateHeaderStyle(PropertyInfo propertyInfo, ExcelExportHeaderInfo info)
            {
                // Header style
                var cellStyle = headerDefaultStyle;

                var headerAttr = propertyInfo.GetHeaderStyleFont<TExportDto>();

                // Apply the default format
                if (CanSetDefaultFormat<TExportDto>(propertyInfo))
                {
                    headerAttr.StyleAttr.DataFormat = SetDefaultDataFormat(typeof(string));
                }

                headerAttr.StyleAttr.ColumnAutoSize = info.Option?.ColumnAutoSize ?? headerAttr.StyleAttr.ColumnAutoSize;
                headerAttr.StyleAttr.ColumnSize = info.Option?.ColumnSize ?? headerAttr.StyleAttr.ColumnSize;

                // Recreate the style when the property declares a style or font attribute.
                if (propertyInfo.HasHeaderStyleAttr() || propertyInfo.HasHeaderFontAttr())
                {
                    cellStyle = CreateHeaderStyleAndFont<TExportDto>(workbook, worksheet, headerAttr.StyleAttr, headerAttr.FontAttr);
                }


                // Add the style output
                return new ExcelCellStyleOutput<TCellStyle, HeaderStyleAttribute, HeaderFontAttribute>(propertyInfo, cellStyle, headerAttr.StyleAttr, headerAttr.FontAttr);
            }
            ExcelCellStyleOutput<TCellStyle, DataStyleAttribute, DataFontAttribute> CreateCellStyle(PropertyInfo propertyInfo, ExcelExportHeaderInfo info)
            {
                // Data style
                var cellStyle = dataDefaultStyle;

                var dataAttr = propertyInfo.GetDataStyleFont<TExportDto>();

                // Apply the default format
                if (info.Option?.DataFormat is not { } dataFormat)
                {
                    if (CanSetDefaultFormat<TExportDto>(propertyInfo) && SetDefaultDataFormat(propertyInfo.PropertyType) is { } defaultDataFormat)
                    {
                        dataAttr.StyleAttr.DataFormat = defaultDataFormat;
                    }
                }
                else
                {
                    dataAttr.StyleAttr.DataFormat = dataFormat;
                }

                // Recreate the style when the property declares a style or font attribute, or when the property type is DateTime.
                if (propertyInfo.HasDataStyleAttr() || propertyInfo.HasDataFontAttr() || propertyInfo.PropertyType.IsDateTime())
                {
                    cellStyle = CreateDataStyleAndFont<TExportDto>(workbook, worksheet, dataAttr.StyleAttr,
                        dataAttr.FontAttr);
                }

                // Add the style output
                return new ExcelCellStyleOutput<TCellStyle, DataStyleAttribute, DataFontAttribute>(propertyInfo, cellStyle, dataAttr.StyleAttr, dataAttr.FontAttr);
            }
        }

        /// <summary>
        /// Gets the default data format
        /// </summary>
        /// <param name="type">The data type</param>
        /// <returns></returns>
        private string? SetDefaultDataFormat(Type type)
        {
            if (type.IsDateTime())
            {
                return "yyyy-MM-dd HH:mm:ss";
            }

            return null;
        }

        /// <summary>
        /// Determines whether the default data format can be applied
        /// </summary>
        /// <typeparam name="TExportDto"></typeparam>
        /// <param name="propertyInfo"></param>
        /// <returns></returns>
        private bool CanSetDefaultFormat<TExportDto>(PropertyInfo propertyInfo) where TExportDto : class
        {
            var dataDefaultAttr = typeof(TExportDto).GetDataStyleFont<TExportDto>();

            var style = propertyInfo.GetDataStyleAttr<TExportDto>();

            // Apply the default format when:
            // 1. The property has a style attribute but its format is empty.
            // 2. The property has no style attribute and the type-level format is empty.
            if (propertyInfo.HasDataStyleAttr() && string.IsNullOrWhiteSpace(style.DataFormat) ||
                !propertyInfo.HasDataStyleAttr() && string.IsNullOrWhiteSpace(dataDefaultAttr.StyleAttr.DataFormat))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the header properties
        /// </summary>
        /// <typeparam name="TExportDto"></typeparam>
        /// <returns></returns>
        private PropertyInfo[] GetHeaderProperties<TExportDto>() where TExportDto : class
        {
            return ExcelPropertyResolver.GetProperties<TExportDto>();
        }

        /// <summary>
        /// Processes footer statistics
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="dataStartRowIndex">The data start row index (zero-based)</param>
        /// <param name="nextRowIndex">The next row index (zero-based)</param>
        /// <param name="infoBundles">The header style bundles</param>
        private void ProcessFooterStatistics<TExportDto>(TWorkbook workbook, TSheet worksheet, int dataStartRowIndex,
            int nextRowIndex,
            List<ExcelExportHeaderInfoBundle<TCellStyle, HeaderStyleAttribute, HeaderFontAttribute, DataStyleAttribute,
                DataFontAttribute>> infoBundles) where TExportDto : class
        {
            var dataEndRowIndex = nextRowIndex - 1;

            var properties = GetHeaderProperties<TExportDto>().Select(a => a.Name).ToList();
            var pNames = infoBundles.Select(p => p.Header.PropertyInfo.Name).ToList();

            for (var i = 0; i < infoBundles.Count; i++)
            {
                var info = infoBundles[i].Header;
                var columnIndex = i;
                var p = info.PropertyInfo;
                var headerStyle = infoBundles[i].HeaderStyle!;
                var dataStyle = infoBundles[i].DataStyle!;

                // Formula settings
                var fxAttrs = p.GetCustomAttributes<ColumnSummaryAttribute>();
                foreach (var fxAttr in fxAttrs)
                {
                    var targetPropertyName = fxAttr.ShowOnColumnPropertyName;
                    if (!string.IsNullOrWhiteSpace(targetPropertyName) && !properties.Contains(targetPropertyName))
                    {
                        throw new Exception($"特性【{nameof(ColumnSummaryAttribute)}】上指定的属性【{targetPropertyName}】在类【{typeof(TExportDto).Name}】中未找到");
                    }

                    var pIndex = string.IsNullOrWhiteSpace(targetPropertyName) ? -1 : pNames.IndexOf(targetPropertyName);
                    var pRowIndex = nextRowIndex + fxAttr.OffsetRow;
                    var pColumnIndex = pIndex == -1 ? columnIndex : pIndex;
                 
                    var func = (ColumnSummaryFunction) fxAttr.Function;

                    if (fxAttr.IsShowLabel)
                    {
                        // Get the label text
                        fxAttr.Label ??=
                            $"{info.HeaderName} {typeof(ColumnSummaryFunction).GetField(func.ToString())?.GetCustomAttribute<DisplayAttribute>()?.Name}";
                        if (!string.IsNullOrWhiteSpace(fxAttr.Unit))
                        {
                            fxAttr.Label += $"（{fxAttr.Unit}）";
                        }

                        // Process the label text

                        var textCell = CreateCell(workbook, worksheet, pRowIndex, pColumnIndex);

                        SetCellValue(workbook, worksheet, textCell, typeof(string), fxAttr.Label);

                        // Apply the label cell style and font using the header style
                        SetHeaderCellStyleAndFont<TExportDto>(workbook, worksheet, textCell, headerStyle);

                        pRowIndex++;
                    }

                    if (func.Equals(ColumnSummaryFunction.None))
                    {
                        continue;
                    }

                    // Process the formula value
                    var cell = CreateCell(workbook, worksheet, pRowIndex, pColumnIndex);

                    // Set the formula
                    var formula = GetCellFormula(workbook, worksheet, func, dataStartRowIndex, dataEndRowIndex, columnIndex, columnIndex);
                    SetCellFormula(workbook, worksheet, cell, formula);

                    // Apply the statistic cell style and font using the data style
                    SetDataCellStyleAndFont<TExportDto>(workbook, worksheet, cell, dataStyle);
                }
            }
        }

        /// <summary>
        /// Gets the formula string
        /// </summary>
        /// <param name="workbook"></param>
        /// <param name="worksheet"></param>
        /// <param name="functionEnum"></param>
        /// <param name="fromRowIndex"></param>
        /// <param name="toRowIndex"></param>
        /// <param name="fromColumnIndex"></param>
        /// <param name="toColumnIndex"></param>
        /// <returns></returns>
        private string GetCellFormula(TWorkbook workbook, TSheet worksheet, ColumnSummaryFunction functionEnum, int fromRowIndex, int toRowIndex, int fromColumnIndex, int toColumnIndex)
        {
            var startAddress = GetCellAddress(workbook, worksheet, fromRowIndex, fromColumnIndex);
            var endAddress = GetCellAddress(workbook, worksheet, toRowIndex, toColumnIndex);

            return functionEnum switch
            {
                ColumnSummaryFunction.Sum => $"SUM({startAddress}:{endAddress})",
                ColumnSummaryFunction.Avg => $"AVERAGE({startAddress}:{endAddress})",
                ColumnSummaryFunction.Count => $"COUNT({startAddress}:{endAddress})",
                ColumnSummaryFunction.Max => $"MAX({startAddress}:{endAddress})",
                ColumnSummaryFunction.Min => $"MIN({startAddress}:{endAddress})",
                ColumnSummaryFunction.None => throw new InvalidOperationException("ColumnSummaryFunction.None does not have a formula."),
                _ => throw new Exception($"函数类型值【{functionEnum}】还未设置公式")
            };
        }



        #endregion

        #region Abstract methods

        /// <summary>
        /// Gets the workbook [Step 1]
        /// </summary>
        /// <param name="options">The export options</param>
        /// <returns></returns>
        protected abstract TWorkbook GetWorkbook(ExcelExportOptions options);

        /// <summary>
        /// Creates the worksheet [Step 2]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="options">The export options</param>
        /// <returns></returns>
        protected abstract TSheet CreateSheet(TWorkbook workbook, ExcelExportOptions options);

        /// <summary>
        /// Creates the cell [Step 3]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="rowIndex">The row index (zero-based)</param>
        /// <param name="columnIndex">The column index (zero-based)</param>
        /// <returns></returns>
        protected abstract TCell CreateCell(TWorkbook workbook, TSheet worksheet, int rowIndex, int columnIndex);

        /// <summary>
        /// Sets the cell value [Step 4]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="cell">The cell</param>
        /// <param name="valueType">The cell value type</param>
        /// <param name="value">The cell value</param>
        protected abstract void SetCellValue(TWorkbook workbook, TSheet worksheet, TCell cell, Type valueType,
            object value);

        /// <summary>
        /// Creates the header style and font [Step 5]
        /// </summary>
        /// <typeparam name="TExportDto">The element type in the collection</typeparam>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="styleAttr">The style attribute</param>
        /// <param name="fontAttr">The font attribute</param>
        /// <returns></returns>
        protected abstract TCellStyle CreateHeaderStyleAndFont<TExportDto>(TWorkbook workbook, TSheet worksheet,
            HeaderStyleAttribute styleAttr, HeaderFontAttribute fontAttr);

        /// <summary>
        /// Creates the data style and font [Step 6]
        /// </summary>
        /// <typeparam name="TExportDto">The element type in the collection</typeparam>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="styleAttr">The style attribute</param>
        /// <param name="fontAttr">The font attribute</param>
        /// <returns></returns>
        protected abstract TCellStyle CreateDataStyleAndFont<TExportDto>(TWorkbook workbook, TSheet worksheet,
            DataStyleAttribute styleAttr, DataFontAttribute fontAttr);

        /// <summary>
        /// Sets the header cell style and font [Step 7]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="cell">The cell</param>
        /// <param name="cellStyleInfo">The cell style metadata</param>
        protected abstract void SetHeaderCellStyleAndFont<TExportDto>(TWorkbook workbook, TSheet worksheet, TCell cell, ExcelCellStyleOutput<TCellStyle, HeaderStyleAttribute, HeaderFontAttribute> cellStyleInfo);

        /// <summary>
        /// Sets the data cell style and font [Step 8]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="cell">The cell</param>
        /// <param name="cellStyleInfo">The cell style metadata</param>
        protected abstract void SetDataCellStyleAndFont<TExportDto>(TWorkbook workbook, TSheet worksheet, TCell cell, ExcelCellStyleOutput<TCellStyle, DataStyleAttribute, DataFontAttribute> cellStyleInfo);

        /// <summary>
        /// Sets the column width [Step 9]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="columnIndex">The column index (zero-based)</param>
        /// <param name="columnSize">The width in characters. Valid range: [0-255].</param>
        /// <param name="columnAutoSize">Whether to auto-fit the column</param>
        protected abstract void SetColumnWidth(TWorkbook workbook, TSheet worksheet, int columnIndex, int columnSize,
            bool columnAutoSize);

        /// <summary>
        /// Sets the row height [Step 10]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="rowIndex">The row index (zero-based)</param>
        /// <param name="rowHeight">The row height in points. Valid range: [0-409].</param>
        protected abstract void SetRowHeight(TWorkbook workbook, TSheet worksheet, int rowIndex, short rowHeight);

        /// <summary>
        /// Sets the merged region [Step 11]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="fromRowIndex">The start row index (zero-based)</param>
        /// <param name="toRowIndex">The end row index (zero-based)</param>
        /// <param name="fromColumnIndex">The start column index (zero-based)</param>
        /// <param name="toColumnIndex">The end column index (zero-based)</param>
        protected abstract void SetMergedRegion(TWorkbook workbook, TSheet worksheet, int fromRowIndex, int toRowIndex, int fromColumnIndex, int toColumnIndex);

        /// <summary>
        /// Gets the cell address text, such as A1 [Step 12]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="rowIndex">The row index (zero-based)</param>
        /// <param name="columnIndex">The column index (zero-based)</param>
        /// <returns></returns>
        protected abstract string GetCellAddress(TWorkbook workbook, TSheet worksheet, int rowIndex, int columnIndex);

        /// <summary>
        /// Sets the cell formula for statistics [Step 13]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <param name="cell">The cell</param>
        /// <param name="cellFormula">The cell formula string</param>
        protected abstract void SetCellFormula(TWorkbook workbook, TSheet worksheet, TCell cell, string cellFormula);

        /// <summary>
        /// Converts the processed workbook to bytes [Step 13]
        /// </summary>
        /// <param name="workbook">The workbook</param>
        /// <param name="worksheet">The worksheet</param>
        /// <returns></returns>
        protected abstract byte[] GetAsByteArray(TWorkbook workbook, TSheet worksheet);

        #endregion

    }
}
