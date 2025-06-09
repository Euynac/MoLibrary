using MoLibrary.Office.Excel.Models;
using MoLibrary.StateStore.ProgressBar;

namespace MoLibrary.Office.Excel
{
    /// <summary>
    /// Excel 导出服务
    /// </summary>
    public interface IExcelExportManager
    {
        /// <summary>
        /// 获取导出的表头信息
        /// </summary>
        /// <typeparam name="TExportDto">导出的dto类</typeparam>
        /// <returns></returns>
        List<ExcelExportHeaderOutput> GetExportHeader<TExportDto>() where TExportDto : class, new();

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
        byte[] Export<TExportDto>(List<TExportDto> data, Action<ExcelExportOptions>? optionAction = null, string[]? onlyExportHeaderName = null)
            where TExportDto : class, new();

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
        Task<byte[]> ExportAsync<TExportDto>(List<TExportDto> data, Action<ExcelExportOptions>? optionAction = null, string[]? onlyExportHeaderName = null) where TExportDto : class, new();

        /// <summary>
        /// 带进度条的异步导出
        /// </summary>
        /// <typeparam name="TExportDto"><paramref name="data"/> 集合中元素的类（按 <typeparamref name="TExportDto"/> 字段顺序导出）</typeparam>
        /// <param name="data">数据</param>
        /// <param name="progressBar">进度条实例，如果为null则不显示进度</param>
        /// <param name="optionAction">配置选项</param>
        /// <param name="onlyExportHeaderName">只需要导出的表头名称
        /// <para>1.不指定则按 <typeparamref name="TExportDto"/> 字段顺序导出全部，指定则按数组顺序导出</para>
        /// <para>2.表头名称 HeaderName 可使用 <see cref="GetExportHeader{TExportDto}"/> 方法获取</para>
        /// </param>
        /// <returns></returns>
        Task<byte[]> ExportWithProgressAsync<TExportDto>(List<TExportDto> data, ProgressBar? progressBar = null, Action<ExcelExportOptions>? optionAction = null, string[]? onlyExportHeaderName = null) where TExportDto : class, new();

        /// <summary>
        /// 创建用于Excel导出的进度条
        /// </summary>
        /// <param name="taskId">任务ID，如果为null则自动生成</param>
        /// <param name="totalRecords">总记录数</param>
        /// <param name="settingAction">进度条设置</param>
        /// <returns></returns>
        Task<ProgressBar> CreateExportProgressBarAsync(string? taskId = null, int totalRecords = 0, Action<ProgressBarSetting>? settingAction = null);
    }
}
