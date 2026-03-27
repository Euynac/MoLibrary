namespace Monica.StateStore.ProgressBar;

/// <summary>
/// Custom progress bar status example
/// </summary>
public class CustomProgressBarStatus(int totalSteps, string id) : ProgressBarStatus(totalSteps, id)
{
    /// <summary>
    /// Custom property: Number of files processed
    /// </summary>
    public int ProcessedFiles { get; set; }

    /// <summary>
    /// Custom property: Total bytes processed
    /// </summary>
    public long ProcessedBytes { get; set; }

    /// <summary>
    /// Custom attribute: the file name currently being processed
    /// </summary>
    public string? CurrentFileName { get; set; }
}

/// <summary>
/// Custom progress bar example
/// </summary>
public class CustomProgressBar(ProgressBarSetting setting, IMoProgressBarService service, string taskId)
    : ProgressBar<CustomProgressBarStatus>(setting, service, taskId)
{
    

    /// <summary>
    /// Update file processing progress
    /// </summary>
    /// <param name="fileName">The name of the file currently being processed</param>
    /// <param name="fileSize">file size</param>
    /// <returns></returns>
    public async Task UpdateFileProgressAsync(string fileName, long fileSize)
    {
        var customStatus = CustomStatus;
        customStatus.ProcessedFiles++;
        customStatus.ProcessedBytes += fileSize;
        customStatus.CurrentFileName = fileName;

        await IncrementAsync(1, $"处理文件: {fileName}", "文件处理阶段");
    }

    public override CustomProgressBarStatus CreateDefaultCustomStatus()
    {
        return new CustomProgressBarStatus(Setting.TotalSteps, taskId);
    }
}

/// <summary>
/// Usage example
/// </summary>
public class ProgressBarExample
{
    private readonly IMoProgressBarService _progressBarService;

    public ProgressBarExample(IMoProgressBarService progressBarService)
    {
        _progressBarService = progressBarService;
    }

    /// <summary>
    /// Basic usage example: stage and status tracking
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
            // initialization phase
            await progressBar.UpdatePhaseAsync("初始化", "准备开始处理...");

            // Data loading phase
            await progressBar.UpdatePhaseAsync("数据加载", "正在加载数据...");
            for (int i = 1; i <= 30; i++)
            {
                await progressBar.UpdateStatusAsync(i, $"加载数据项 {i}/30");
                await Task.Delay(100); // 模拟处理时间
            }

            // Data processing stage
            await progressBar.UpdatePhaseAsync("数据处理", "正在处理数据...");
            for (int i = 31; i <= 80; i++)
            {
                await progressBar.UpdateStatusAsync(i, $"处理数据项 {i-30}/50");
                await Task.Delay(50); // 模拟处理时间
            }

            // completion stage
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
    /// Custom status usage example
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

            // Simulate processing of multiple files
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
    /// Get status example
    /// </summary>
    public async Task GetStatusExample()
    {
        // Get basic status
        var basicStatus = await RequireStatusAsync("basic-example");
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

        // Get custom status
        var customStatus = await _progressBarService.GetProgressBarStatusAsync<CustomProgressBarStatus>("custom-example");
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
    /// Cancel operation example
    /// </summary>
    public async Task CancelExample()
    {
        var progressBar = await _progressBarService.CreateProgressBarAsync("cancel-example");

        // Listen for cancellation events
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

            // Simulate cancellation operation
            await progressBar.CancelTaskAsync("用户主动取消");
        }
        catch (InvalidOperationException)
        {
            // Task has been canceled
            Console.WriteLine("任务已被取消");
        }

        // Get status later to view cancellation information
        var status = await _progressBarService.GetProgressBarStatusAsync("cancel-example");
        if (status == null)
        {
            throw new InvalidOperationException("Progress bar 'cancel-example' was not found.");
        }
        Console.WriteLine($"从存储获取的取消状态: {status.IsCancelled}");
        Console.WriteLine($"从存储获取的取消原因: {status.CancelReason}");
    }

    /// <summary>
    /// Cross-microservice status check example
    /// </summary>
    public async Task CrossServiceStatusCheckExample()
    {
        // Simulate microservice A creation task
        var progressBarA = await _progressBarService.CreateProgressBarAsync("cross-service-task", setting =>
        {
            setting.TotalSteps = 50;
        });

        // Start processing
        await progressBarA.UpdatePhaseAsync("数据处理", "开始处理...");
        await progressBarA.UpdateStatusAsync(10, "处理了10项");

        // Simulate microservice B to check task status
        var taskStatus = await RequireStatusAsync("cross-service-task");
        Console.WriteLine($"微服务B检查: 进度 {taskStatus.Percentage}%, 阶段: {taskStatus.Phase}");
        
        if (taskStatus.IsCancelled)
        {
            Console.WriteLine("微服务B发现任务已被取消，停止相关处理");
            return;
        }

        // After simulating for a period of time, microservice A cancels the task
        await progressBarA.CancelTaskAsync("检测到异常，主动取消");

        // Microservice B checks the status again
        var updatedStatus = await RequireStatusAsync("cross-service-task");
        if (updatedStatus.IsCancelled)
        {
            Console.WriteLine($"微服务B检测到任务已被取消: {updatedStatus.CancelReason}");
            // Cleanup logic can be executed here
        }
    }

    private async Task<ProgressBarStatus> RequireStatusAsync(string id)
    {
        return await _progressBarService.GetProgressBarStatusAsync(id)
            ?? throw new InvalidOperationException($"Progress bar '{id}' was not found.");
    }
} 
