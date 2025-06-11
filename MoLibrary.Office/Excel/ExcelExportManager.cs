using MoLibrary.Office.Excel.Models;
using MoLibrary.StateStore.ProgressBar;

namespace MoLibrary.Office.Excel
{
    /// <summary>
    /// Excel 导出服务
    /// </summary>
    public abstract class ExcelExportManager : IMoExcelExportManager
    {
        /// <summary>
        /// 构造
        /// </summary>
        protected ExcelExportManager()
        {
        }

        /// <summary>
        /// 获取导出的表头信息
        /// </summary>
        /// <typeparam name="TExportDto">导出的dto类</typeparam>
        /// <returns></returns>
        public List<ExcelExportHeaderOutput> GetExportHeader<TExportDto>() where TExportDto : class
        {
            return ExcelHelper.GetProperties<TExportDto>().Select(a => new ExcelExportHeaderOutput
            {
                HeaderName = a.GetDisplayNameFromProperty()
            }).ToList();
        }

        /// <summary>
        /// 导出
        /// </summary>
        /// <typeparam name="TExportDto"><paramref name="data"/> 集合中元素的类（按 <typeparamref name="TExportDto"/> 字段顺序导出）</typeparam>
        /// <param name="data">数据</param>
        /// <param name="optionAction">配置选项</param>
        /// <param name="onlyExportHeaderName">只需要导出的表头名称
        ///     <para>1.不指定则按 <typeparamref name="TExportDto"/> 字段顺序导出全部，指定则按数组顺序导出</para>
        ///     <para>2.表头名称 HeaderName 可使用 <see cref="GetExportHeader{TExportDto}"/> 方法获取</para>
        /// </param>
        /// <param name="progressBar">进度条实例，可选，为null时不报告进度</param>
        /// <returns></returns>
        public byte[] Export<TExportDto>(IReadOnlyList<TExportDto> data,
            Action<ExcelExportOptions>? optionAction = null, string[]? onlyExportHeaderName = null,
            ProgressBar? progressBar = null) where TExportDto : class
        {
            try
            {
                return ImplementExport(data, optionAction, onlyExportHeaderName, progressBar);
            }
            catch (Exception e)
            {
                progressBar?.CancelTaskAsync($"导出失败: {e.Message}").Wait();
                throw new Exception(e.Message, e);
            }
        }

        /// <summary>
        /// 导出
        /// </summary>
        /// <typeparam name="TExportDto"><paramref name="data"/> 集合中元素的类（按 <typeparamref name="TExportDto"/> 字段顺序导出）</typeparam>
        /// <param name="data">数据</param>
        /// <param name="optionAction">配置选项</param>
        /// <param name="onlyExportHeaderName">只需要导出的表头名称
        ///     <para>1.不指定则按 <typeparamref name="TExportDto"/> 字段顺序导出全部，指定则按数组顺序导出</para>
        ///     <para>2.表头名称 HeaderName 可使用 <see cref="GetExportHeader{TExportDto}"/> 方法获取</para>
        /// </param>
        /// <param name="progressBar">进度条实例，可选，为null时不报告进度</param>
        /// <returns></returns>
        public async Task<byte[]> ExportAsync<TExportDto>(IReadOnlyList<TExportDto> data,
            Action<ExcelExportOptions>? optionAction = null, string[]? onlyExportHeaderName = null,
            ProgressBar? progressBar = null) where TExportDto : class
        {
            return await Task.FromResult(Export(data, optionAction, onlyExportHeaderName, progressBar));
        }

        /// <summary>
        /// 导出实现
        /// </summary>
        /// <typeparam name="TExportDto"><paramref name="data"/> 集合中元素的类（导出的表头顺序为字段顺序）</typeparam>
        /// <param name="data">数据</param>
        /// <param name="optionAction">配置选项</param>
        /// <param name="onlyExportHeaderName">只需要导出的表头名称
        ///     <para>1.不指定则按 <typeparamref name="TExportDto"/> 字段顺序导出全部，指定则按数组顺序导出</para>
        ///     <para>2.表头名称 HeaderName 可使用 <see cref="GetExportHeader{TExportDto}"/> 方法获取</para>
        /// </param>
        /// <param name="progressBar">进度条（可选）</param>
        /// <returns></returns>
        protected abstract byte[] ImplementExport<TExportDto>(IReadOnlyList<TExportDto> data,
            Action<ExcelExportOptions> optionAction, string[]? onlyExportHeaderName, ProgressBar? progressBar = null)
            where TExportDto : class;
    }
}
