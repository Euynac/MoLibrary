using MoLibrary.Office.Excel.Models;
using MoLibrary.StateStore.ProgressBar;

namespace MoLibrary.Office.Excel
{
    /// <summary>
    /// Excel 导出服务
    /// </summary>
    public abstract class ExcelExportManager : IExcelExportManager
    {
        private readonly IMoProgressBarService? _progressBarService;

        /// <summary>
        /// 构造
        /// </summary>
        protected ExcelExportManager()
        {
        }

        /// <summary>
        /// 构造（带进度条服务）
        /// </summary>
        /// <param name="progressBarService">进度条服务</param>
        protected ExcelExportManager(IMoProgressBarService progressBarService)
        {
            _progressBarService = progressBarService;
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
        public async Task<byte[]> ExportWithProgressAsync<TExportDto>(List<TExportDto> data, ProgressBar? progressBar = null, Action<ExcelExportOptions>? optionAction = null, string[]? onlyExportHeaderName = null) where TExportDto : class, new()
        {
            try
            {
                return await ImplementExportWithProgress(data, progressBar, optionAction, onlyExportHeaderName);
            }
            catch (Exception e)
            {
                if (progressBar != null)
                {
                    await progressBar.CancelTaskAsync($"导出失败：{e.Message}");
                }
                throw new Exception(e.Message, e);
            }
        }

        /// <summary>
        /// 创建用于Excel导出的进度条
        /// </summary>
        /// <param name="taskId">任务ID，如果为null则自动生成</param>
        /// <param name="totalRecords">总记录数</param>
        /// <param name="settingAction">进度条设置</param>
        /// <returns></returns>
        public async Task<ProgressBar> CreateExportProgressBarAsync(string? taskId = null, int totalRecords = 0, Action<ProgressBarSetting>? settingAction = null)
        {
            if (_progressBarService == null)
            {
                throw new InvalidOperationException("进度条服务未注册，请在构造函数中注入 IMoProgressBarService");
            }

            return await _progressBarService.CreateProgressBarAsync(taskId, setting =>
            {
                // 设置默认值：表头处理(10%) + 数据处理(80%) + 最终处理(10%)
                setting.TotalSteps = Math.Max(totalRecords + 20, 100); // 至少100步确保有意义的进度显示
                setting.AutoUpdateDuration = TimeSpan.FromSeconds(2); // 每2秒自动保存状态
                setting.TimeToLive = TimeSpan.FromMinutes(30); // 30分钟后过期
                setting.CompletedTimeToLive = TimeSpan.FromMinutes(5); // 完成后5分钟清理
                
                // 执行用户自定义设置
                settingAction?.Invoke(setting);
            });
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
        /// <param name="progressBar">进度条实例</param>
        /// <param name="optionAction">配置选项</param>
        /// <param name="onlyExportHeaderName">只需要导出的表头名称</param>
        /// <returns></returns>
        protected virtual async Task<byte[]> ImplementExportWithProgress<TExportDto>(List<TExportDto> data, ProgressBar? progressBar, Action<ExcelExportOptions>? optionAction, string[]? onlyExportHeaderName)
            where TExportDto : class, new()
        {
            // 默认实现：调用同步方法但添加进度汇报
            if (progressBar != null)
            {
                await progressBar.UpdateStatusAsync(0, "开始Excel导出", "初始化");
                
                // 由于同步方法无法中间汇报进度，这里先设置50%进度
                await progressBar.UpdateStatusAsync(progressBar.Status.TotalSteps / 2, "正在生成Excel文件", "数据处理");
            }
            
            var result = ImplementExport(data, optionAction, onlyExportHeaderName);
            
            if (progressBar != null)
            {
                await progressBar.CompleteTaskAsync();
            }
            
            return result;
        }
    }
}
