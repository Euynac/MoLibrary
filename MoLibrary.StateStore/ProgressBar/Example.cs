namespace MoLibrary.StateStore.ProgressBar;

/// <summary>
/// 自定义进度条状态示例
/// </summary>
public class CustomProgressBarStatus : ProgressBarStatus
{
    public CustomProgressBarStatus(int totalSteps) : base(totalSteps)
    {
    }

    /// <summary>
    /// 自定义属性：处理的文件数量
    /// </summary>
    public int ProcessedFiles { get; set; }

    /// <summary>
    /// 自定义属性：处理的总字节数
    /// </summary>
    public long ProcessedBytes { get; set; }

    /// <summary>
    /// 自定义属性：当前处理的文件名
    /// </summary>
    public string? CurrentFileName { get; set; }
}

/// <summary>
/// 自定义进度条示例
/// </summary>
public class CustomProgressBar : ProgressBar
{
    public CustomProgressBar(ProgressBarSetting setting, IMoProgressBarService service, string taskId)
        : base(setting, service, taskId)
    {
        // 使用自定义状态替换默认状态
        Status = new CustomProgressBarStatus(setting.TotalSteps);
    }

    /// <summary>
    /// 获取自定义状态
    /// </summary>
    public new CustomProgressBarStatus Status { get; private set; }

    /// <summary>
    /// 更新文件处理进度
    /// </summary>
    /// <param name="fileName">当前处理的文件名</param>
    /// <param name="fileSize">文件大小</param>
    /// <returns></returns>
    public async Task UpdateFileProgressAsync(string fileName, long fileSize)
    {
        var customStatus = Status;
        customStatus.ProcessedFiles++;
        customStatus.ProcessedBytes += fileSize;
        customStatus.CurrentFileName = fileName;

        await IncrementAsync(1, $"处理文件: {fileName}", "文件处理阶段");
    }
}

/// <summary>
/// 使用示例
/// </summary>
public class ProgressBarExample
{
    private readonly IMoProgressBarService _progressBarService;

    public ProgressBarExample(IMoProgressBarService progressBarService)
    {
        _progressBarService = progressBarService;
    }

    /// <summary>
    /// 基本使用示例：阶段和状态跟踪
    /// </summary>
    public async Task BasicUsageExample()
    {
        var progressBar = await _progressBarService.CreateProgressBarAsync("basic-example", setting =>
        {
            setting.TotalSteps = 100;
            setting.AutoUpdateDuration = TimeSpan.FromSeconds(5);
        });

        try
        {
            // 初始化阶段
            await progressBar.UpdatePhaseAsync("初始化", "准备开始处理...");

            // 数据加载阶段
            await progressBar.UpdatePhaseAsync("数据加载", "正在加载数据...");
            for (int i = 1; i <= 30; i++)
            {
                await progressBar.UpdateStatusAsync(i, $"加载数据项 {i}/30");
                await Task.Delay(100); // 模拟处理时间
            }

            // 数据处理阶段
            await progressBar.UpdatePhaseAsync("数据处理", "正在处理数据...");
            for (int i = 31; i <= 80; i++)
            {
                await progressBar.UpdateStatusAsync(i, $"处理数据项 {i-30}/50");
                await Task.Delay(50); // 模拟处理时间
            }

            // 完成阶段
            await progressBar.UpdatePhaseAsync("完成", "正在保存结果...");
            for (int i = 81; i <= 100; i++)
            {
                await progressBar.UpdateStatusAsync(i, $"保存结果 {i-80}/20");
                await Task.Delay(30); // 模拟处理时间
            }

            await progressBar.CompleteTaskAsync();
        }
        catch (Exception)
        {
            await progressBar.CancelTaskAsync("处理过程中发生错误");
            throw;
        }
    }

    /// <summary>
    /// 自定义状态使用示例
    /// </summary>
    public async Task CustomStatusExample()
    {
        var progressBar = await _progressBarService.CreateProgressBarAsync<CustomProgressBar>("custom-example", setting =>
        {
            setting.TotalSteps = 10;
        });

        try
        {
            await progressBar.UpdatePhaseAsync("文件处理", "开始处理文件...");

            // 模拟处理多个文件
            var files = new[] { "file1.txt", "file2.txt", "file3.txt" };
            foreach (var file in files)
            {
                await progressBar.UpdateFileProgressAsync(file, 1024 * 1024); // 1MB文件
                await Task.Delay(1000); // 模拟处理时间
            }

            await progressBar.CompleteTaskAsync();
        }
        catch (Exception)
        {
            await progressBar.CancelTaskAsync("文件处理失败");
            throw;
        }
    }

    /// <summary>
    /// 获取状态示例
    /// </summary>
    public async Task GetStatusExample()
    {
        // 获取基本状态
        var basicStatus = await _progressBarService.GetProgressBarStatus("basic-example");
        Console.WriteLine($"基本进度: {basicStatus.Percentage}%, 阶段: {basicStatus.Phase}, 状态: {basicStatus.CurrentStatus}");
        
        if (basicStatus.IsCancelled)
        {
            Console.WriteLine($"任务已取消，原因: {basicStatus.CancelReason}");
        }
        else if (basicStatus.Percentage >= 100)
        {
            Console.WriteLine("任务已完成");
        }
        else
        {
            Console.WriteLine("任务正在进行中");
        }

        // 获取自定义状态
        var customStatus = await _progressBarService.GetProgressBarStatus<CustomProgressBarStatus>("custom-example");
        if (customStatus != null)
        {
            Console.WriteLine($"自定义进度: {customStatus.Percentage}%");
            Console.WriteLine($"处理文件数: {customStatus.ProcessedFiles}");
            Console.WriteLine($"处理字节数: {customStatus.ProcessedBytes}");
            Console.WriteLine($"当前文件: {customStatus.CurrentFileName}");
            Console.WriteLine($"当前阶段: {customStatus.Phase}");
            Console.WriteLine($"是否已取消: {customStatus.IsCancelled}");
        }
    }

    /// <summary>
    /// 取消操作示例
    /// </summary>
    public async Task CancelExample()
    {
        var progressBar = await _progressBarService.CreateProgressBarAsync("cancel-example");

        // 监听取消事件
        progressBar.Cancelled += (sender, e) =>
        {
            Console.WriteLine($"任务被取消: {e.Reason}");
            Console.WriteLine($"状态中的取消原因: {e.Status.Status.CancelReason}");
            Console.WriteLine($"状态中的取消标记: {e.Status.Status.IsCancelled}");
        };

        try
        {
            await progressBar.UpdatePhaseAsync("处理中", "正在执行任务...");
            await Task.Delay(2000);

            // 模拟取消操作
            await progressBar.CancelTaskAsync("用户主动取消");
        }
        catch (InvalidOperationException)
        {
            // 任务已被取消
            Console.WriteLine("任务已被取消");
        }

        // 稍后获取状态查看取消信息
        var status = await _progressBarService.GetProgressBarStatus("cancel-example");
        Console.WriteLine($"从存储获取的取消状态: {status.IsCancelled}");
        Console.WriteLine($"从存储获取的取消原因: {status.CancelReason}");
    }

    /// <summary>
    /// 跨微服务状态检查示例
    /// </summary>
    public async Task CrossServiceStatusCheckExample()
    {
        // 模拟微服务A创建任务
        var progressBarA = await _progressBarService.CreateProgressBarAsync("cross-service-task", setting =>
        {
            setting.TotalSteps = 50;
        });

        // 开始处理
        await progressBarA.UpdatePhaseAsync("数据处理", "开始处理...");
        await progressBarA.UpdateStatusAsync(10, "处理了10项");

        // 模拟微服务B检查任务状态
        var taskStatus = await _progressBarService.GetProgressBarStatus("cross-service-task");
        Console.WriteLine($"微服务B检查: 进度 {taskStatus.Percentage}%, 阶段: {taskStatus.Phase}");
        
        if (taskStatus.IsCancelled)
        {
            Console.WriteLine("微服务B发现任务已被取消，停止相关处理");
            return;
        }

        // 模拟一段时间后，微服务A取消了任务
        await progressBarA.CancelTaskAsync("检测到异常，主动取消");

        // 微服务B再次检查状态
        var updatedStatus = await _progressBarService.GetProgressBarStatus("cross-service-task");
        if (updatedStatus.IsCancelled)
        {
            Console.WriteLine($"微服务B检测到任务已被取消: {updatedStatus.CancelReason}");
            // 在这里可以执行清理逻辑
        }
    }
} 