namespace MoLibrary.Repository;

public class MoRepositoryOptions
{
    /// <summary>
    /// Use User-defined function mapping to filter data.
    /// https://learn.microsoft.com/en-us/ef/core/querying/user-defined-function-mapping
    /// </summary>
    public bool UseDbFunction { get; set; }

    /// <summary>
    /// 自动注册DbContext Factory
    /// </summary>
    public bool UseDbContextFactory { get; set; }

    /// <summary>
    /// 是否启用敏感数据日志。默认为null，表示当环境为Development时启用。
    /// </summary>
    public bool? EnableSensitiveDataLogging { get; set; }

    public const int ConcurrencyStampMaxLength = 40;
}