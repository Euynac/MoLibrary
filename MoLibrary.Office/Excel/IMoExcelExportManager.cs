using MoLibrary.Office.Excel.Models;
using MoLibrary.StateStore.ProgressBar;

namespace MoLibrary.Office.Excel
{
    /// <summary>
    /// Excel 导出服务
    /// </summary>
    /// TODO 当前EXCEL模块实现不支持动态的DTO类型，因其内部大量使用泛型实现，无法动态获取类型。另外需要尽量减少TExportDto的泛型限制
    public interface IMoExcelExportManager
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
        ///     <para>1.不指定则按 <typeparamref name="TExportDto"/> 字段顺序导出全部，指定则按数组顺序导出</para>
        ///     <para>2.表头名称 HeaderName 可使用 <see cref="GetExportHeader{TExportDto}"/> 方法获取</para>
        /// </param>
        /// <param name="progressBar">进度条实例，可选，为null时不报告进度</param>
        /// <returns></returns>
        byte[] Export<TExportDto>(IReadOnlyList<TExportDto> data, Action<ExcelExportOptions>? optionAction = null,
            string[]? onlyExportHeaderName = null, ProgressBar? progressBar = null)
            where TExportDto : class, new();

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
        Task<byte[]> ExportAsync<TExportDto>(IReadOnlyList<TExportDto> data,
            Action<ExcelExportOptions>? optionAction = null, string[]? onlyExportHeaderName = null,
            ProgressBar? progressBar = null) where TExportDto : class, new();
    }
}
