using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using MoLibrary.Office.Excel.Attributes;
using MoLibrary.Office.Excel.Models;
using MoLibrary.StateStore.ProgressBar;

namespace MoLibrary.Office.Excel
{
    /// <summary>
    /// 导出基类
    /// </summary>
    public abstract class ExcelExportBase<TWorkbook, TSheet, TRow, TCell, TCellStyle>
    {
        /// <summary>
        /// 构造
        /// </summary>
        protected ExcelExportBase()
        {
        }

        /// <summary>
        /// 导出
        /// </summary>
        /// <typeparam name="TExportDto"><paramref name="data"/> 集合中元素的类（导出的表头顺序为字段顺序）</typeparam>
        /// <param name="data">数据</param>
        /// <param name="optionAction">配置选项</param>
        /// <param name="requests">只需要导出的表头名称（指定则按 <typeparamref name="TExportDto"/> 字段顺序导出全部，不指定空则按数组顺序导出）</param>
        /// <param name="progressBar">进度条实例，可选，为null时不报告进度</param>
        /// <returns></returns>
        public byte[] Export<TExportDto>(IReadOnlyList<TExportDto> data, Action<ExcelExportOptions>? optionAction,
            ExcelHeaderRequest[] requests, ProgressBar? progressBar = null)
            where TExportDto : class
        {
            try
            {
                progressBar?.ThrowIfCancellationRequested();
                var options = new ExcelExportOptions();
                optionAction?.Invoke(options);
                options.CheckError();


                progressBar?.IncrementAsync(1, "初始化Excel导出", "导出Excel").Wait();

                //获取工作册
                var workbook = GetWorkbook(options);
                progressBar?.IncrementAsync(4, "创建工作册").Wait();

                //创建工作表
                var worksheet = CreateSheet(workbook, options);

                progressBar?.IncrementAsync(5, "创建工作表").Wait();

                //验证表头，并获取要导出的表头信息
                var headers = CheckHeader<TExportDto>(requests, options);

                progressBar?.IncrementAsync(5, "验证表头").Wait();

                //表头行下标
                var headerRowIndex = options.HeaderRowIndex - 1;

                //先获取需要的表头列的样式和字体
                var infoBundle = GetHeaderColumnStyleAndFont<TExportDto>(workbook, worksheet, headers);

                progressBar?.IncrementAsync(10, "创建表头及数据样式").Wait();

                //处理表头单元格
                ProcessHeaderCell<TExportDto>(workbook, worksheet, headerRowIndex, infoBundle);
                progressBar?.IncrementAsync(10, "处理表头").Wait();

                //数据起始行下标
                var dataRowIndex = options.DataRowStartIndex - 1;

                progressBar?.IncrementAsync(5, "创建数据样式").Wait();

                //处理数据单元格
                ProcessDataCell(workbook, worksheet, data, dataRowIndex, infoBundle, out var footerRowIndex, progressBar, 50);

                //处理底部数据统计
                progressBar?.IncrementAsync(5, "处理数据统计").Wait();
                ProcessFooterStatistics<TExportDto>(workbook, worksheet, dataRowIndex, footerRowIndex, infoBundle);


                //处理列宽【有数据才能处理自动列宽，所以必须放到最后进行处理】
                progressBar?.IncrementAsync(5, "处理列宽").Wait();
                ProcessColumnWidth<TExportDto>(workbook, worksheet, headers);


                //转换并获取工作册字节
                progressBar?.IncrementAsync(5, "生成Excel文件").Wait();
                var result = GetAsByteArray(workbook, worksheet);
                progressBar?.IncrementAsync(0, "Excel导出完成").Wait();

                return result;
            }
            catch (Exception e)
            {
                progressBar?.CancelTaskAsync($"导出失败: {e.Message}").Wait();
                throw new Exception(e.Message, e);
            }
        }

        /// <summary>
        /// 验证表头，并获取要导出的表头信息
        /// </summary>
        /// <typeparam name="TExportDto"></typeparam>
        /// <param name="requests">只需要导出的表头名称（指定则按 <typeparamref name="TExportDto"/> 字段顺序导出全部，不指定空则按数组顺序导出）</param>
        /// <param name="options"></param>
        public ExcelExportHeaderInfo[] CheckHeader<TExportDto>(ExcelHeaderRequest[] requests,
            ExcelExportOptions options) where TExportDto : class
        {
            return ExcelHelper.CheckHeader<TExportDto>(requests, options.DisallowDuplicateHeader);
        }

        #region 私有

        /// <summary>
        /// 处理表头单元格
        /// </summary>
        /// <typeparam name="TExportDto"></typeparam>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="headerRowIndex">表头行下标（起始下标：0）</param>
        /// <param name="infoBundle">列样式集合</param>
        private void ProcessHeaderCell<TExportDto>(TWorkbook workbook, TSheet worksheet, int headerRowIndex, List<ExcelExportHeaderInfoBundle<TCellStyle, HeaderStyleAttribute, HeaderFontAttribute, DataStyleAttribute, DataFontAttribute>> infoBundle) where TExportDto : class
        {
            //处理单元格 值、样式、字体、行高
            for (var columnIndex = 0; columnIndex < infoBundle.Count; columnIndex++)
            {
                var info = infoBundle[columnIndex].Header;
                var headerStyle = infoBundle[columnIndex].HeaderStyle!;
                var p = info.PropertyInfo;

                //创建单元格
                var cell = CreateCell(workbook, worksheet, headerRowIndex, columnIndex);

                //处理表头单元格值
                ProcessHeaderCellValue(workbook, worksheet, cell, p, info.HeaderName);

                //处理表头单元格样式和字体
                SetHeaderCellStyleAndFont<TExportDto>(workbook, worksheet, cell, headerStyle);
            }

            //处理表头行 行高（必须先创建行，才能处理）
            ProcessRowHeight<TExportDto>(workbook, worksheet, headerRowIndex, true);
        }

        /// <summary>
        /// 处理数据单元格
        /// </summary>
        /// <typeparam name="TExportDto"><paramref name="data"/>集合中元素的类</typeparam>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="data">数据集合</param>
        /// <param name="rowIndex">下一行下标（起始下标： 0）</param>
        /// <param name="infoBundle">数据样式</param>
        /// <param name="nextRowIndex">下一行下标（起始下标： 0）</param>
        /// <param name="progressBar">进度条（可选）</param>
        /// <param name="progressWeight">数据处理在整体进度中的权重（仅当progressBar不为空时有效）</param>
        /// <returns>下一行下标（从0开始）</returns>
        private void ProcessDataCell<TExportDto>(TWorkbook workbook, TSheet worksheet,
            IReadOnlyList<TExportDto> data, int rowIndex,
            List<ExcelExportHeaderInfoBundle<TCellStyle, HeaderStyleAttribute, HeaderFontAttribute, DataStyleAttribute,
                DataFontAttribute>> infoBundle,
            out int nextRowIndex, ProgressBar? progressBar = null, int progressWeight = 0)
            where TExportDto : class
        {
            //可合并行区域信息
            var rowMergedList = new List<ExcelExportMergedRegionInfo>();
            var rowMergedHeader = GetHeaderProperties<TExportDto>().Where(a => a.GetCustomAttribute<MergeRowAttribute>() != null).Select(a => a.Name).ToList();

            //可合并列区域信息
            var columnMergedList = new List<ExcelExportMergedRegionInfo>();
            var columnMergedHeader = typeof(TExportDto).GetCustomAttributes<MergeColumnAttribute>().Select(a => a.PropertyNames.Distinct().ToArray()).Where(a => a.Length > 1).ToList();

            //验证合并特性值
            CheckMergeAttribute<TExportDto>(columnMergedHeader);

            // 进度条相关变量
            var dataCount = data.Count;
            var initialStep = progressBar?.Status.CurrentStep;
            var progressIncrement = dataCount > 0 && progressBar != null ? progressWeight / (double) dataCount : 0;

            progressBar?.UpdatePhaseAsync("处理数据", $"开始处理数据，共 {dataCount} 条").Wait();

            //处理单元格 值、样式、字体、合并
            var processedCount = 0;
            foreach (var d in data)
            {
                progressBar?.ThrowIfCancellationRequested();

                for (var columnIndex = 0; columnIndex < infoBundle.Count; columnIndex++)
                {
                    var info = infoBundle[columnIndex].Header;
                    var dataStyle = infoBundle[columnIndex].DataStyle!;
                    var p = info.PropertyInfo;

                    //创建单元格
                    var cell = CreateCell(workbook, worksheet, rowIndex, columnIndex);

                    //处理数据单元格值
                    var value = p.GetValue(d);
                    ProcessDataCellValue(workbook, worksheet, cell, p, value);

                    //处理数据单元格样式和字体
                    SetDataCellStyleAndFont<TExportDto>(workbook, worksheet, cell, dataStyle);

                    //处理列合并
                    ProcessMergeColumn(columnMergedHeader, columnMergedList, rowIndex, columnIndex, p, value);

                    //处理行合并
                    ProcessMergeRow(rowMergedHeader, rowMergedList, rowIndex, columnIndex, p, value);
                }

                //处理数据行 行高（必须先创建行，才能处理）
                ProcessRowHeight<TExportDto>(workbook, worksheet, rowIndex, false);

                //下一行下标
                rowIndex++;

                // 更新进度（如果有进度条）
                if (progressBar != null)
                {
                    processedCount++;
                    if (processedCount % 50 == 0 || processedCount == dataCount) // 每处理50行或处理完所有数据更新一次进度
                    {
                        progressBar.UpdateStatusAsync(initialStep!.Value + (int) (progressIncrement * processedCount), $"已处理 {processedCount}/{dataCount} 条数据").Wait();
                    }
                }
            }

            //下一行下标
            nextRowIndex = rowIndex;

            //移除不能合并的
            columnMergedList.RemoveAll(m => !m.IsCanMergedColumn());
            rowMergedList.RemoveAll(m => !m.IsCanMergedRow());

            //若该属性存在列合并，则移除所有行合（优先列合并）
            rowMergedList.RemoveAll(m => columnMergedList.Any(a => a.PropertyNames.Intersect(m.PropertyNames).Any()));

            //合并单元格区域
            progressBar?.IncrementAsync(0, "合并单元格").Wait();

            //所有合并信息
            var mergedRegion = rowMergedList.Concat(columnMergedList).ToList();

            //处理数据合并区域
            foreach (var m in mergedRegion)
            {
                SetMergedRegion(workbook, worksheet, m.FromRowIndex, m.ToRowIndex, m.FromColumnIndex, m.ToColumnIndex);
            }

            progressBar?.IncrementAsync(0, "数据处理完成").Wait();
        }

        /// <summary>
        /// 验证合并特性值
        /// </summary>
        /// <typeparam name="TExportDto">集合中元素的类</typeparam>
        /// <param name="columnMergedHeader">列合并表头信息</param>
        /// <returns>下一行下标（从0开始）</returns>
        private void CheckMergeAttribute<TExportDto>(IReadOnlyList<string[]> columnMergedHeader) where TExportDto : class
        {
            var className = typeof(TExportDto).Name;
            var properties = GetHeaderProperties<TExportDto>();
            var attrName = nameof(MergeColumnAttribute);

            //验证：不存在属性名称，单个属性在多个特性中重复，同一特性中属性类型不一致
            for (var index = 0; index < columnMergedHeader.Count; index++)
            {
                var names = columnMergedHeader[index];
                var num = index + 1;

                //不存在属性名称
                var noExist = names.Where(a => properties.All(p => p.Name != a)).Select(a => a);
                if (noExist.Any())
                {
                    throw new Exception(
                        $"类【{className}】的第 {num} 个 {attrName} 指定的属性名称未找到：{string.Join(",", noExist)}");
                }

                //同一特性中属性类型不一致
                var type = names.Select(n => properties.First(b => n == b.Name).PropertyType);
                if (type.Distinct().Count() > 1)
                {
                    throw new Exception(
                        $"类【{className}】的第 {num} 个 {attrName} 指定的属性类型不一致");
                }
            }

            //单个属性在多个特性中重复
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
        /// 处理行合并
        /// </summary>
        /// <param name="rowMergedHeader">行合并表头</param>
        /// <param name="mergedList">合并信息集合</param>
        /// <param name="rowIndex">当前行下标（起始下标：0）</param>
        /// <param name="columnIndex">当前列下标（起始下标：0）</param>
        /// <param name="propertyInfo">字段属性</param>
        /// <param name="value">值</param>
        private void ProcessMergeRow(IReadOnlyList<string> rowMergedHeader, ICollection<ExcelExportMergedRegionInfo> mergedList, int rowIndex, int columnIndex, PropertyInfo propertyInfo, object? value)
        {
            if (rowMergedHeader.All(a => a != propertyInfo.Name))
            {
                return;
            }

            //获取最后一个当前列的合并信息
            var merge = mergedList.LastOrDefault(a => a.PropertyNames.Contains(propertyInfo.Name));

            //值是否相等
            var isValueEqual = merge?.IsValueEqual(value) == true;

            //无该列合并信息、不是同一列，值不相等但可合并、值相等但不是相邻行 都要新建合并信息
            if (merge == null || !merge.IsSameColumn(columnIndex) || !isValueEqual && merge.IsCanMergedRow() || isValueEqual && !merge.IsSiblingRow(rowIndex))
            {
                mergedList.Add(new ExcelExportMergedRegionInfo
                {
                    PropertyNames = [propertyInfo.Name],
                    Value = value,
                    FromRowIndex = rowIndex,
                    ToRowIndex = rowIndex,
                    FromColumnIndex = columnIndex,
                    ToColumnIndex = columnIndex
                });
            }
            else if (!isValueEqual) //不相等，则替换掉
            {
                merge.Value = value;
                merge.FromRowIndex = rowIndex;
                merge.ToRowIndex = rowIndex;
                merge.FromColumnIndex = columnIndex;
                merge.ToColumnIndex = columnIndex;
            }
            else //值相等，相邻行 ，则改变合并行下标
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
        /// 处理列合并
        /// </summary>
        /// <param name="columnMergedHeader">列合并表头</param>
        /// <param name="mergedList">合并信息集合</param>
        /// <param name="rowIndex">当前行下表（起始下标：0）</param>
        /// <param name="columnIndex">当前列下标（起始下标：0）</param>
        /// <param name="propertyInfo">字段属性</param>
        /// <param name="value">值</param>
        private void ProcessMergeColumn(IReadOnlyList<string[]>? columnMergedHeader, ICollection<ExcelExportMergedRegionInfo> mergedList, int rowIndex, int columnIndex, PropertyInfo propertyInfo, object? value)
        {
            //处理列合并
            if (columnMergedHeader == null || !columnMergedHeader.Any(a => a.Contains(propertyInfo.Name)))
            {
                return;
            }

            //获取最后一个当前列的合并信息
            var merge = mergedList.LastOrDefault(a => a.PropertyNames.Contains(propertyInfo.Name));

            //值是否相等
            var isValueEqual = merge?.IsValueEqual(value) == true;

            //无该列合并信息、不是同一行、值不相等但可合并、值相等但不是相邻列 都要新建合并信息
            if (merge == null || !merge.IsSameRow(rowIndex) || !isValueEqual && merge.IsCanMergedColumn() || isValueEqual && !merge.IsSiblingColumn(columnIndex))
            {
                mergedList.Add(new ExcelExportMergedRegionInfo
                {
                    PropertyNames = columnMergedHeader.First(a => a.Contains(propertyInfo.Name)),
                    Value = value,
                    FromRowIndex = rowIndex,
                    ToRowIndex = rowIndex,
                    FromColumnIndex = columnIndex,
                    ToColumnIndex = columnIndex
                });
            }
            else if (!isValueEqual) //值不相等，则替换掉
            {
                merge.Value = value;
                merge.FromRowIndex = rowIndex;
                merge.ToRowIndex = rowIndex;
                merge.FromColumnIndex = columnIndex;
                merge.ToColumnIndex = columnIndex;
            }
            else  //值相等，相邻列 ，则改变合并列下标
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
        /// 处理列宽（必须先创建列，才能处理；列宽自动调整，必须有列数据才能处理）
        /// </summary>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="headers">要导出的表头信息</param>

        private void ProcessColumnWidth<TExportDto>(TWorkbook workbook, TSheet worksheet, ExcelExportHeaderInfo[] headers) where TExportDto : class
        {
            for (var i = 0; i < headers.Length; i++)
            {
                var info = headers[i];
                var columnIndex = i;
                var p = info.PropertyInfo;

                //设置列宽
                var styleAttr = p.GetHeaderStyleAttr<TExportDto>();
                SetColumnWidth(workbook, worksheet, columnIndex, styleAttr.ColumnSize, styleAttr.ColumnAutoSize);
            }
        }

        /// <summary>
        /// 处理行高（必须先创建行，才能处理）
        /// </summary>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="rowIndex">行下表</param>
        /// <param name="isHeader">是否表头</param>

        private void ProcessRowHeight<TExportDto>(TWorkbook workbook, TSheet worksheet, int rowIndex, bool isHeader) where TExportDto : class
        {
            var attr = typeof(TExportDto).GetCustomAttribute<RowHeightAttribute>() ?? new RowHeightAttribute();

            SetRowHeight(workbook, worksheet, rowIndex, isHeader ? attr.HeaderRowHeight : attr.DataRowHeight);
        }

        /// <summary>
        /// 处理表头单元格值
        /// </summary>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="cell">单元格</param>
        /// <param name="propertyInfo">当前正在处理的字段属性</param>
        /// <param name="value">字段值</param>
        private void ProcessHeaderCellValue(TWorkbook workbook, TSheet worksheet, TCell cell, PropertyInfo propertyInfo, object value)
        {
            try
            {
                //设置单元格值
                SetCellValue(workbook, worksheet, cell, typeof(string), value);

            }
            catch (Exception e)
            {
                throw new Exception($"【{propertyInfo.Name}】的表头值设置出错：{e.Message}", e);
            }

        }

        /// <summary>
        /// 处理数据单元格值
        /// </summary>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="cell">单元格</param>
        /// <param name="propertyInfo">当前正在处理的字段属性</param>
        /// <param name="value">字段值</param>
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
                    //设置单元格值
                    SetCellValue(workbook, worksheet, cell, propertyInfo.PropertyType, value);
                }

            }
            catch (Exception e)
            {
                throw new Exception($"【{propertyInfo.Name}】的数据值设置出错：{e.Message}", e);
            }

        }

        /// <summary>
        /// 获取所有表头列及其数据的样式和字体
        /// </summary>
        /// <typeparam name="TExportDto"></typeparam>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="headers"></param>
        private List<ExcelExportHeaderInfoBundle<TCellStyle, HeaderStyleAttribute, HeaderFontAttribute, DataStyleAttribute, DataFontAttribute>> GetHeaderColumnStyleAndFont<TExportDto>(TWorkbook workbook,
                TSheet worksheet, ExcelExportHeaderInfo[] headers) where TExportDto : class
        {
            var styles = new List<ExcelExportHeaderInfoBundle<TCellStyle, HeaderStyleAttribute, HeaderFontAttribute, DataStyleAttribute, DataFontAttribute>>();

            //表头默认样式
            var headerDefaultAttr = typeof(TExportDto).GetHeaderStyleFont<TExportDto>();
            var headerDefaultStyle =
                CreateHeaderStyleAndFont<TExportDto>(workbook, worksheet, headerDefaultAttr.StyleAttr, headerDefaultAttr.FontAttr);

            //数据默认样式
            var dataDefaultAttr = typeof(TExportDto).GetDataStyleFont<TExportDto>();
            var dataDefaultStyle = CreateDataStyleAndFont<TExportDto>(workbook, worksheet, dataDefaultAttr.StyleAttr,
                dataDefaultAttr.FontAttr);

            foreach (var info in headers)
            {
                var propertyInfo = info.PropertyInfo;
              
                //添加
                styles.Add(new ExcelExportHeaderInfoBundle<TCellStyle, HeaderStyleAttribute, HeaderFontAttribute, DataStyleAttribute, DataFontAttribute>(info)
                {
                    HeaderStyle = CreateHeaderStyle(propertyInfo, info),
                    DataStyle = CreateCellStyle(propertyInfo, info)
                });

            }

            return styles;
            ExcelCellStyleOutput<TCellStyle, HeaderStyleAttribute, HeaderFontAttribute> CreateHeaderStyle(PropertyInfo propertyInfo, ExcelExportHeaderInfo info)
            {
                //表头样式
                var cellStyle = headerDefaultStyle;

                var headerAttr = propertyInfo.GetHeaderStyleFont<TExportDto>();

                //设置默认格式化
                if (CanSetDefaultFormat<TExportDto>(propertyInfo))
                {
                    headerAttr.StyleAttr.DataFormat = SetDefaultDataFormat(typeof(string));
                }

                headerAttr.StyleAttr.ColumnAutoSize = info.Option?.ColumnAutoSize ?? headerAttr.StyleAttr.ColumnAutoSize;
                headerAttr.StyleAttr.ColumnSize = info.Option?.ColumnSize ?? headerAttr.StyleAttr.ColumnSize;

                //属性上有样式、有字体样式，则重新创建样式
                if (propertyInfo.HasHeaderStyleAttr() || propertyInfo.HasHeaderFontAttr())
                {
                    cellStyle = CreateHeaderStyleAndFont<TExportDto>(workbook, worksheet, headerAttr.StyleAttr, headerAttr.FontAttr);
                }


                //添加
                return new ExcelCellStyleOutput<TCellStyle, HeaderStyleAttribute, HeaderFontAttribute>(propertyInfo, cellStyle, headerAttr.StyleAttr, headerAttr.FontAttr);
            }
            ExcelCellStyleOutput<TCellStyle, DataStyleAttribute, DataFontAttribute> CreateCellStyle(PropertyInfo propertyInfo, ExcelExportHeaderInfo info)
            {
                //数据样式
                var cellStyle = dataDefaultStyle;

                var dataAttr = propertyInfo.GetDataStyleFont<TExportDto>();

                //设置默认格式化
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

                //属性上有样式、有字体样式、属性为时间 则重新创建样式
                if (propertyInfo.HasDataStyleAttr() || propertyInfo.HasDataFontAttr() || propertyInfo.PropertyType.IsDateTime())
                {
                    cellStyle = CreateDataStyleAndFont<TExportDto>(workbook, worksheet, dataAttr.StyleAttr,
                        dataAttr.FontAttr);
                }

                //添加
                return new ExcelCellStyleOutput<TCellStyle, DataStyleAttribute, DataFontAttribute>(propertyInfo, cellStyle, dataAttr.StyleAttr, dataAttr.FontAttr);
            }
        }

        /// <summary>
        /// 设置默认数据格式化
        /// </summary>
        /// <param name="type">数据类型</param>
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
        /// 是否可以设置默认数据格式
        /// </summary>
        /// <typeparam name="TExportDto"></typeparam>
        /// <param name="propertyInfo"></param>
        /// <returns></returns>
        private bool CanSetDefaultFormat<TExportDto>(PropertyInfo propertyInfo) where TExportDto : class
        {
            var dataDefaultAttr = typeof(TExportDto).GetDataStyleFont<TExportDto>();

            var style = propertyInfo.GetDataStyleAttr<TExportDto>();

            //设置默认格式化：1.属性上有样式，但格式化为空，2.属性上没有样式，类上格式化为空
            if (propertyInfo.HasDataStyleAttr() && string.IsNullOrWhiteSpace(style.DataFormat) ||
                !propertyInfo.HasDataStyleAttr() && string.IsNullOrWhiteSpace(dataDefaultAttr.StyleAttr.DataFormat))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取表头属性
        /// </summary>
        /// <typeparam name="TExportDto"></typeparam>
        /// <returns></returns>
        private PropertyInfo[] GetHeaderProperties<TExportDto>() where TExportDto : class
        {
            return ExcelHelper.GetProperties<TExportDto>();
        }

        /// <summary>
        /// 处理底部数据统计
        /// </summary>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="dataStartRowIndex">数据起始行下标（起始下标：0）</param>
        /// <param name="nextRowIndex">下一行下标（起始下标：0）</param>
        /// <param name="infoBundles">表头样式</param>
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

                //公式
                var fxAttrs = p.GetCustomAttributes<ColumnStatsAttribute>();
                foreach (var fxAttr in fxAttrs)
                {
                    if (!string.IsNullOrWhiteSpace(fxAttr.ShowOnColumnPropertyName) && !properties.Contains(fxAttr.ShowOnColumnPropertyName))
                    {
                        throw new Exception($"特性【{nameof(ColumnStatsAttribute)}】上指定的属性【{fxAttr.ShowOnColumnPropertyName}】在类【{typeof(TExportDto).Name}】中未找到");
                    }

                    var pIndex = pNames.IndexOf(fxAttr.ShowOnColumnPropertyName);
                    var pRowIndex = nextRowIndex + fxAttr.OffsetRow;
                    var pColumnIndex = pIndex == -1 ? columnIndex : pIndex;
                 
                    var func = (FunctionEnum) fxAttr.Function;

                    if (fxAttr.IsShowLabel)
                    {
                        //获取标签文本
                        fxAttr.Label ??=
                            $"{info.HeaderName} {typeof(FunctionEnum).GetField(func.ToString())?.GetCustomAttribute<DisplayAttribute>()?.Name}";
                        if (!string.IsNullOrWhiteSpace(fxAttr.Unit))
                        {
                            fxAttr.Label += $"（{fxAttr.Unit}）";
                        }

                        //处理标签文本

                        var textCell = CreateCell(workbook, worksheet, pRowIndex, pColumnIndex);

                        SetCellValue(workbook, worksheet, textCell, typeof(string), fxAttr.Label);

                        //处理标签文本单元格样式和字体（采用表头样式）
                        SetHeaderCellStyleAndFont<TExportDto>(workbook, worksheet, textCell, headerStyle);

                        pRowIndex++;
                    }

                    if (func.Equals(FunctionEnum.None))
                    {
                        continue;
                    }

                    //处理公式值
                    var cell = CreateCell(workbook, worksheet, pRowIndex, pColumnIndex);

                    //设置公式
                    var formula = GetCellFormula(workbook, worksheet, func, dataStartRowIndex, dataEndRowIndex, columnIndex, columnIndex);
                    SetCellFormula(workbook, worksheet, cell, formula);

                    //处理统计单元格样式和字体（采用数据样式）
                    SetDataCellStyleAndFont<TExportDto>(workbook, worksheet, cell, dataStyle);
                }
            }
        }

        /// <summary>
        /// 获取公式字符串
        /// </summary>
        /// <param name="workbook"></param>
        /// <param name="worksheet"></param>
        /// <param name="functionEnum"></param>
        /// <param name="fromRowIndex"></param>
        /// <param name="toRowIndex"></param>
        /// <param name="fromColumnIndex"></param>
        /// <param name="toColumnIndex"></param>
        /// <returns></returns>
        private string GetCellFormula(TWorkbook workbook, TSheet worksheet, FunctionEnum functionEnum, int fromRowIndex, int toRowIndex, int fromColumnIndex, int toColumnIndex)
        {
            var startAddress = GetCellAddress(workbook, worksheet, fromRowIndex, fromColumnIndex);
            var endAddress = GetCellAddress(workbook, worksheet, toRowIndex, toColumnIndex);

            string formula = null;

            switch (functionEnum)
            {
                case FunctionEnum.None:
                    {
                        formula = null;
                        break;
                    }
                case FunctionEnum.Sum:
                    {
                        formula = $"SUM({startAddress}:{endAddress})";
                        break;
                    }
                case FunctionEnum.Avg:
                    {
                        formula = $"AVERAGE({startAddress}:{endAddress})";
                        break;
                    }
                case FunctionEnum.Count:
                    {
                        formula = $"COUNT({startAddress}:{endAddress})";
                        break;
                    }
                case FunctionEnum.Max:
                    {
                        formula = $"MAX({startAddress}:{endAddress})";
                        break;
                    }
                case FunctionEnum.Min:
                    {
                        formula = $"MIN({startAddress}:{endAddress})";
                        break;
                    }
                default:
                    throw new Exception($"函数类型值【{functionEnum}】还未设置公式");
            }

            return formula;
        }



        #endregion

        #region 抽象方法

        /// <summary>
        /// 获取工作册【步骤 1】
        /// </summary>
        /// <param name="options">配置选项</param>
        /// <returns></returns>
        protected abstract TWorkbook GetWorkbook(ExcelExportOptions options);

        /// <summary>
        /// 创建工作表【步骤 2】
        /// </summary>
        /// <param name="workbook">工作册</param>
        /// <param name="options">配置选项</param>
        /// <returns></returns>
        protected abstract TSheet CreateSheet(TWorkbook workbook, ExcelExportOptions options);

        /// <summary>
        /// 创建单元格【步骤 3】
        /// </summary>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="rowIndex">行下标（起始下标： 0）</param>
        /// <param name="columnIndex">列下标（起始下标： 0）</param>
        /// <returns></returns>
        protected abstract TCell CreateCell(TWorkbook workbook, TSheet worksheet, int rowIndex, int columnIndex);

        /// <summary>
        /// 设置数据单元格值【步骤 4】
        /// </summary>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="cell">单元格</param>
        /// <param name="valueType">单元格的值类型</param>
        /// <param name="value">单元格值</param>
        protected abstract void SetCellValue(TWorkbook workbook, TSheet worksheet, TCell cell, Type valueType,
            object value);

        /// <summary>
        /// 创建表头样式和字体【步骤 5】
        /// </summary>
        /// <typeparam name="TExportDto">集合中元素的类</typeparam>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="styleAttr">样式特征</param>
        /// <param name="fontAttr">字体特征</param>
        /// <returns></returns>
        protected abstract TCellStyle CreateHeaderStyleAndFont<TExportDto>(TWorkbook workbook, TSheet worksheet,
            HeaderStyleAttribute styleAttr, HeaderFontAttribute fontAttr);

        /// <summary>
        /// 创建数据样式和字体【步骤 6】
        /// </summary>
        /// <typeparam name="TExportDto">集合中元素的类</typeparam>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="styleAttr">样式特征</param>
        /// <param name="fontAttr">字体特征</param>
        /// <returns></returns>
        protected abstract TCellStyle CreateDataStyleAndFont<TExportDto>(TWorkbook workbook, TSheet worksheet,
            DataStyleAttribute styleAttr, DataFontAttribute fontAttr);

        /// <summary>
        /// 设置表头单元格样式和字体【步骤 7】
        /// </summary>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="cell">单元格</param>
        /// <param name="cellStyleInfo">单元格样式信息</param>
        protected abstract void SetHeaderCellStyleAndFont<TExportDto>(TWorkbook workbook, TSheet worksheet, TCell cell, ExcelCellStyleOutput<TCellStyle, HeaderStyleAttribute, HeaderFontAttribute> cellStyleInfo);

        /// <summary>
        /// 设置数据单元格样式和字体【步骤 8】
        /// </summary>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="cell">单元格</param>
        /// <param name="cellStyleInfo">单元格样式信息</param>
        protected abstract void SetDataCellStyleAndFont<TExportDto>(TWorkbook workbook, TSheet worksheet, TCell cell, ExcelCellStyleOutput<TCellStyle, DataStyleAttribute, DataFontAttribute> cellStyleInfo);

        /// <summary>
        /// 设置列宽【步骤 9】
        /// </summary>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="columnIndex">列下标（起始下标： 0）</param>
        /// <param name="columnSize">宽度（单位：字符，取值区间：[0-255]）</param>
        /// <param name="columnAutoSize">是否自动调整</param>
        protected abstract void SetColumnWidth(TWorkbook workbook, TSheet worksheet, int columnIndex, int columnSize,
            bool columnAutoSize);

        /// <summary>
        /// 设置行高【步骤 10】
        /// </summary>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="rowIndex">行下标（起始下标： 0）</param>
        /// <param name="rowHeight">行高（单位：磅，取值区间：[0-409]）</param>
        protected abstract void SetRowHeight(TWorkbook workbook, TSheet worksheet, int rowIndex, short rowHeight);

        /// <summary>
        /// 设置合并区域【步骤 11】
        /// </summary>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="fromRowIndex">起始行下标（起始下标： 0）</param>
        /// <param name="toRowIndex">结束行下标（起始下标： 0）</param>
        /// <param name="fromColumnIndex">起始列下标（起始下标： 0）</param>
        /// <param name="toColumnIndex">结束列下标（起始下标： 0）</param>
        protected abstract void SetMergedRegion(TWorkbook workbook, TSheet worksheet, int fromRowIndex, int toRowIndex, int fromColumnIndex, int toColumnIndex);

        /// <summary>
        /// 获取单元格地址文本（如：A1）【步骤 12】
        /// </summary>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="rowIndex">行下标（起始下标： 0）</param>
        /// <param name="columnIndex">列下标（起始下标： 0）</param>
        /// <returns></returns>
        protected abstract string GetCellAddress(TWorkbook workbook, TSheet worksheet, int rowIndex, int columnIndex);

        /// <summary>
        /// 设置单元格公式（统计）【步骤 13】
        /// </summary>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <param name="cell">单元格</param>
        /// <param name="cellFormula">单元格公式字符串</param>
        protected abstract void SetCellFormula(TWorkbook workbook, TSheet worksheet, TCell cell, string cellFormula);

        /// <summary>
        /// 把处理好的工作册转换为字节【步骤 13】
        /// </summary>
        /// <param name="workbook">工作册</param>
        /// <param name="worksheet">工作表</param>
        /// <returns></returns>
        protected abstract byte[] GetAsByteArray(TWorkbook workbook, TSheet worksheet);

        #endregion

    }
}
