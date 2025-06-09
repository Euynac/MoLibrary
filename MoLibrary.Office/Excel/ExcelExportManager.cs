using MoLibrary.Office.Excel.Models;
using MoLibrary.StateStore.ProgressBar;

namespace MoLibrary.Office.Excel
{
    /// <summary>
    /// Excel 导出服务
    /// </summary>
    public abstract class ExcelExportManager : IExcelExportManager
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
        public List<ExcelExportHeaderOutput> GetExportHeader<TExportDto>() where TExportDto : class, new()
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
        /// <para>1.不指定则按 <typeparamref name="TExportDto"/> 字段顺序导出全部，指定则按数组顺序导出</para>
        /// <para>2.表头名称 HeaderName 可使用 <see cref="GetExportHeader{TExportDto}"/> 方法获取</para>
        /// </param>
        /// <returns></returns>
        public byte[] Export<TExportDto>(List<TExportDto> data, Action<ExcelExportOptions>? optionAction = null, string[]? onlyExportHeaderName = null) where TExportDto : class, new()
        {
            try
            {
                return ImplementExport(data, optionAction, onlyExportHeaderName);
            }
            catch (Exception e)
            {
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
        /// <para>1.不指定则按 <typeparamref name="TExportDto"/> 字段顺序导出全部，指定则按数组顺序导出</para>
        /// <para>2.表头名称 HeaderName 可使用 <see cref="GetExportHeader{TExportDto}"/> 方法获取</para>
        /// </param>
        /// <returns></returns>
        public Task<byte[]> ExportAsync<TExportDto>(List<TExportDto> data, Action<ExcelExportOptions>? optionAction = null, string[]? onlyExportHeaderName = null) where TExportDto : class, new()
        {
            return Task.FromResult(Export(data, optionAction, onlyExportHeaderName));
        }

        /// <summary>
        /// 带进度条的导出
        /// </summary>
        /// <typeparam name="TExportDto"><paramref name="data"/> 集合中元素的类（按 <typeparamref name="TExportDto"/> 字段顺序导出）</typeparam>
        /// <param name="data">数据</param>
        /// <param name="progressBar">进度条实例</param>
        /// <param name="optionAction">配置选项</param>
        /// <param name="onlyExportHeaderName">只需要导出的表头名称
        /// <para>1.不指定则按 <typeparamref name="TExportDto"/> 字段顺序导出全部，指定则按数组顺序导出</para>
        /// <para>2.表头名称 HeaderName 可使用 <see cref="GetExportHeader{TExportDto}"/> 方法获取</para>
        /// </param>
        /// <returns></returns>
        public byte[] Export<TExportDto>(List<TExportDto> data, ProgressBar progressBar, Action<ExcelExportOptions>? optionAction = null, string[]? onlyExportHeaderName = null) where TExportDto : class, new()
        {
            try
            {
                progressBar.UpdateStatusAsync(0, "开始导出Excel").Wait();
                var result = ImplementExport(data, optionAction, onlyExportHeaderName, progressBar);
                progressBar.CompleteTaskAsync().Wait();
                return result;
            }
            catch (Exception e)
            {
                progressBar.CancelTaskAsync($"导出失败: {e.Message}").Wait();
                throw new Exception(e.Message, e);
            }
        }

        /// <summary>
        /// 带进度条的导出
        /// </summary>
        /// <typeparam name="TExportDto"><paramref name="data"/> 集合中元素的类（按 <typeparamref name="TExportDto"/> 字段顺序导出）</typeparam>
        /// <param name="data">数据</param>
        /// <param name="progressBar">进度条实例</param>
        /// <param name="optionAction">配置选项</param>
        /// <param name="onlyExportHeaderName">只需要导出的表头名称
        /// <para>1.不指定则按 <typeparamref name="TExportDto"/> 字段顺序导出全部，指定则按数组顺序导出</para>
        /// <para>2.表头名称 HeaderName 可使用 <see cref="GetExportHeader{TExportDto}"/> 方法获取</para>
        /// </param>
        /// <returns></returns>
        public async Task<byte[]> ExportAsync<TExportDto>(List<TExportDto> data, ProgressBar progressBar, Action<ExcelExportOptions>? optionAction = null, string[]? onlyExportHeaderName = null) where TExportDto : class, new()
        {
            try
            {
                await progressBar.UpdateStatusAsync(0, "开始导出Excel");
                var result = ImplementExport(data, optionAction, onlyExportHeaderName, progressBar);
                await progressBar.CompleteTaskAsync();
                return result;
            }
            catch (Exception e)
            {
                await progressBar.CancelTaskAsync($"导出失败: {e.Message}");
                throw new Exception(e.Message, e);
            }
        }

        /// <summary>
        /// 导出实现
        /// </summary>
        /// <typeparam name="TExportDto"><paramref name="data"/> 集合中元素的类（导出的表头顺序为字段顺序）</typeparam>
        /// <param name="data">数据</param>
        /// <param name="optionAction">配置选项</param>
        /// <param name="onlyExportHeaderName">只需要导出的表头名称
        /// <para>1.不指定则按 <typeparamref name="TExportDto"/> 字段顺序导出全部，指定则按数组顺序导出</para>
        /// <para>2.表头名称 HeaderName 可使用 <see cref="GetExportHeader{TExportDto}"/> 方法获取</para>
        /// </param>
        /// <returns></returns>
        protected abstract byte[] ImplementExport<TExportDto>(List<TExportDto> data, Action<ExcelExportOptions> optionAction, string[] onlyExportHeaderName)
            where TExportDto : class, new();

        /// <summary>
        /// 带进度条的导出实现
        /// </summary>
        /// <typeparam name="TExportDto"><paramref name="data"/> 集合中元素的类（导出的表头顺序为字段顺序）</typeparam>
        /// <param name="data">数据</param>
        /// <param name="optionAction">配置选项</param>
        /// <param name="onlyExportHeaderName">只需要导出的表头名称</param>
        /// <param name="progressBar">进度条</param>
        /// <returns></returns>
        protected virtual byte[] ImplementExport<TExportDto>(List<TExportDto> data, Action<ExcelExportOptions> optionAction, string[] onlyExportHeaderName, ProgressBar progressBar)
            where TExportDto : class, new()
        {
            // 默认实现，子类可以重写此方法提供更精细的进度报告
            return ImplementExport(data, optionAction, onlyExportHeaderName);
        }
    }
}
