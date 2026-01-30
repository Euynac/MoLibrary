namespace Monica.JobScheduler.Models;

public class BatchJobOperationResult
{
    public int TotalRequested { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<JobOperationResultItem> Results { get; set; } = [];
}

public class JobOperationResultItem
{
    public required string JobKey { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
