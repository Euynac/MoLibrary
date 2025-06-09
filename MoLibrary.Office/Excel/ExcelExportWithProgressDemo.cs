using MoLibrary.Office.Excel.Models;
using MoLibrary.StateStore.ProgressBar;
using System.ComponentModel.DataAnnotations;

namespace MoLibrary.Office.Excel
{
    /// <summary>
    /// Excel带进度条导出示例
    /// </summary>
    public class ExcelExportWithProgressDemo
    {
        private readonly IExcelExportManager _excelExportManager;

        public ExcelExportWithProgressDemo(IExcelExportManager excelExportManager)
        {
            _excelExportManager = excelExportManager;
        }

        /// <summary>
        /// 演示基本的进度条导出功能
        /// </summary>
        /// <returns></returns>
        public async Task<(byte[] fileBytes, string taskId)> BasicProgressExportDemo()
        {
            // 1. 准备测试数据
            var testData = GenerateTestData(1000);

            // 2. 创建进度条
            var progressBar = await _excelExportManager.CreateExportProgressBarAsync(
                taskId: "demo-export-task",
                totalRecords: testData.Count,
                settingAction: setting =>
                {
                    setting.AutoUpdateDuration = TimeSpan.FromSeconds(1); // 每秒自动保存状态
                    setting.TimeToLive = TimeSpan.FromMinutes(15); // 15分钟后过期
                }
            );

            try
            {
                // 3. 执行带进度条的导出
                var fileBytes = await _excelExportManager.ExportWithProgressAsync(
                    data: testData,
                    progressBar: progressBar,
                    optionAction: options =>
                    {
                        options.SheetName = "进度条导出示例";
                        options.HeaderRowIndex = 1;
                        options.DataRowStartIndex = 2;
                    }
                );

                return (fileBytes, progressBar.TaskId);
            }
            catch (Exception ex)
            {
                // 发生异常时进度条会自动标记为取消状态
                throw new Exception($"Excel导出失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 演示可取消的进度条导出
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns></returns>
        public async Task<(byte[] fileBytes, string taskId)> CancellableProgressExportDemo(CancellationToken cancellationToken = default)
        {
            var testData = GenerateTestData(5000); // 更大的数据集

            var progressBar = await _excelExportManager.CreateExportProgressBarAsync(
                totalRecords: testData.Count
            );

            // 注册取消事件监听
            progressBar.Cancelled += (sender, e) =>
            {
                Console.WriteLine($"导出任务已取消，原因: {e.Reason}");
            };

            // 注册进度更新事件监听
            progressBar.StatusUpdated += (sender, e) =>
            {
                Console.WriteLine($"导出进度: {e.Status.Status.Percentage:F1}% - {e.Status.Status.CurrentStatus}");
            };

            // 注册完成事件监听
            progressBar.Completed += (sender, e) =>
            {
                Console.WriteLine("Excel导出完成！");
            };

            try
            {
                var fileBytes = await _excelExportManager.ExportWithProgressAsync(
                    data: testData,
                    progressBar: progressBar,
                    optionAction: options =>
                    {
                        options.SheetName = "大数据导出示例";
                    }
                );

                return (fileBytes, progressBar.TaskId);
            }
            catch (OperationCanceledException)
            {
                await progressBar.CancelTaskAsync("用户取消操作");
                throw;
            }
        }

        /// <summary>
        /// 演示如何监控已有的导出任务进度
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns></returns>
        public async Task<ProgressBarStatus?> MonitorExportProgress(string taskId)
        {
            // 如果你的导出管理器支持获取进度条服务
            if (_excelExportManager is ExcelExportManager manager)
            {
                try
                {
                    var progressBarService = GetProgressBarService(manager);
                    var status = await progressBarService.GetProgressBarStatus(taskId);
                    return status;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"获取进度状态失败: {ex.Message}");
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// 生成测试数据
        /// </summary>
        /// <param name="count">数据数量</param>
        /// <returns></returns>
        private List<ExportTestData> GenerateTestData(int count)
        {
            var random = new Random();
            var departments = new[] { "研发部", "销售部", "市场部", "财务部", "人事部" };
            var positions = new[] { "经理", "主管", "专员", "助理", "实习生" };

            return Enumerable.Range(1, count).Select(i => new ExportTestData
            {
                Id = i,
                Name = $"员工{i:D4}",
                Department = departments[random.Next(departments.Length)],
                Position = positions[random.Next(positions.Length)],
                Salary = random.Next(3000, 20000),
                HireDate = DateTime.Now.AddDays(-random.Next(1, 1000)),
                IsActive = random.Next(0, 2) == 1,
                Email = $"employee{i:D4}@company.com"
            }).ToList();
        }

        /// <summary>
        /// 获取进度条服务（这里需要根据具体实现调整）
        /// </summary>
        /// <param name="manager"></param>
        /// <returns></returns>
        private IMoProgressBarService GetProgressBarService(ExcelExportManager manager)
        {
            // 这里需要根据具体的依赖注入实现来获取服务
            // 示例代码，实际使用时需要调整
            throw new NotImplementedException("请根据你的依赖注入容器实现获取 IMoProgressBarService");
        }
    }

    /// <summary>
    /// 测试用的导出数据模型
    /// </summary>
    public class ExportTestData
    {
        [Display(Name = "员工ID")]
        public int Id { get; set; }

        [Display(Name = "姓名")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "部门")]
        public string Department { get; set; } = string.Empty;

        [Display(Name = "职位")]
        public string Position { get; set; } = string.Empty;

        [Display(Name = "薪资")]
        public decimal Salary { get; set; }

        [Display(Name = "入职日期")]
        public DateTime HireDate { get; set; }

        [Display(Name = "是否在职")]
        public bool IsActive { get; set; }

        [Display(Name = "邮箱")]
        public string Email { get; set; } = string.Empty;
    }
} 