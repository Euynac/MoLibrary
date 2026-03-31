using Monica.Office.Excel.Models;
using Monica.StateStore.TaskProgress.Models;

namespace Monica.Office.Excel.Abstractions
{
    /// <summary>
    /// Excel export service
    /// </summary>
    public interface IExcelExporter
    {
        /// <summary>
        /// Gets the export header metadata
        /// </summary>
        /// <typeparam name="TExportDto">The DTO type to export</typeparam>
        /// <returns></returns>
        List<ExcelExportHeaderOutput> GetExportHeader<TExportDto>() where TExportDto : class;

        /// <summary>
        /// Validates export header requests against the export DTO.
        /// </summary>
        /// <typeparam name="TExportDto">The DTO type to export.</typeparam>
        /// <param name="requests">The requested headers. An empty array means all exportable headers.</param>
        /// <param name="disallowDuplicateHeader">Whether duplicate requested headers should be rejected.</param>
        void ValidateHeaders<TExportDto>(ExcelHeaderRequest[] requests, bool disallowDuplicateHeader = false)
            where TExportDto : class;

        /// <summary>
        /// Exports data
        /// </summary>
        /// <typeparam name="TExportDto">The element type in <paramref name="data"/>. Columns are exported in <typeparamref name="TExportDto"/> property order.</typeparam>
        /// <param name="data">The data to export</param>
        /// <param name="requests"></param>
        /// <param name="optionAction">Configures export options</param>
        /// <param name="taskProgress">Optional task progress instance. No progress is reported when <see langword="null"/>.</param>
        /// <returns></returns>
        byte[] Export<TExportDto>(IReadOnlyList<TExportDto> data, ExcelHeaderRequest[] requests, Action<ExcelExportOptions>? optionAction = null, TaskProgress? taskProgress = null)
            where TExportDto : class;

        /// <summary>
        /// Exports data asynchronously
        /// </summary>
        /// <typeparam name="TExportDto">The element type in <paramref name="data"/>. Columns are exported in <typeparamref name="TExportDto"/> property order.</typeparam>
        /// <param name="data">The data to export</param>
        /// <param name="requests"></param>
        /// <param name="optionAction">Configures export options</param>
        /// <param name="taskProgress">Optional task progress instance. No progress is reported when <see langword="null"/>.</param>
        /// <returns></returns>
        Task<byte[]> ExportAsync<TExportDto>(IReadOnlyList<TExportDto> data, ExcelHeaderRequest[] requests, Action<ExcelExportOptions>? optionAction = null, TaskProgress? taskProgress = null)
            where TExportDto : class;
        /// <summary>
        /// Exports data
        /// </summary>
        /// <typeparam name="TExportDto">The element type in <paramref name="data"/>. Columns are exported in <typeparamref name="TExportDto"/> property order.</typeparam>
        /// <param name="data">The data to export</param>
        /// <param name="optionAction">Configures export options</param>
        /// <param name="onlyExportHeaderName">The header names to export
        ///     <para>1. If not specified, all columns are exported in <typeparamref name="TExportDto"/> property order. If specified, columns are exported in array order.</para>
        ///     <para>2. Use <see cref="GetExportHeader{TExportDto}"/> to get available header names.</para>
        /// </param>
        /// <param name="taskProgress">Optional task progress instance. No progress is reported when <see langword="null"/>.</param>
        /// <returns></returns>
        byte[] Export<TExportDto>(IReadOnlyList<TExportDto> data, Action<ExcelExportOptions>? optionAction = null,
            string[]? onlyExportHeaderName = null, TaskProgress? taskProgress = null)
            where TExportDto : class;

        /// <summary>
        /// Exports data asynchronously
        /// </summary>
        /// <typeparam name="TExportDto">The element type in <paramref name="data"/>. Columns are exported in <typeparamref name="TExportDto"/> property order.</typeparam>
        /// <param name="data">The data to export</param>
        /// <param name="optionAction">Configures export options</param>
        /// <param name="onlyExportHeaderName">The header names to export
        ///     <para>1. If not specified, all columns are exported in <typeparamref name="TExportDto"/> property order. If specified, columns are exported in array order.</para>
        ///     <para>2. Use <see cref="GetExportHeader{TExportDto}"/> to get available header names.</para>
        /// </param>
        /// <param name="taskProgress">Optional task progress instance. No progress is reported when <see langword="null"/>.</param>
        /// <returns></returns>
        Task<byte[]> ExportAsync<TExportDto>(IReadOnlyList<TExportDto> data,
            Action<ExcelExportOptions>? optionAction = null, string[]? onlyExportHeaderName = null,
            TaskProgress? taskProgress = null) where TExportDto : class;
    }
}
