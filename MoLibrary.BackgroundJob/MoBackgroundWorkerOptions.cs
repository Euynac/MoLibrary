namespace MoLibrary.BackgroundJob;

public class MoBackgroundWorkerOptions
{
    /// <summary>
    /// 除Entry程序集之外，额外自动注册涉及的程序集
    /// </summary>
    public string[]? RelatedAssemblies { get; set; }

    /// <summary>
    /// 禁用作业自动注册
    /// </summary>
    public bool DisableAutoRegister { get; set; }


    #region Hangfire
    public HashSet<string> Queues { get; set; } = ["default"];
    public List<string> SupportedProject { get; set; } = [];
    public bool DisplayStorageConnectionString { get; set; }

    /// <summary>
    /// The Title displayed on the dashboard, optionally modify to describe this dashboards purpose.
    /// </summary>
    public string DashboardTitle { get; set; } = "APP";

    public MoBackgroundWorkerRedisOptions? RedisOptions { get; set; }

    public bool UseInMemoryStorage { get; set; }
    #endregion
}


public class MoBackgroundWorkerRedisOptions
{
    /// <summary>
    /// 作业Redis地址
    /// </summary>
    public string RedisHost { get; set; } = "";
    public int RedisPort { get; set; }
    public string RedisPassword { get; set; } = "";

    /// <summary>
    /// 连接类型
    /// </summary>
    public ERedisConnectionType ConnectionType { get; set; }

    /// <summary>
    /// The service name used to resolve a service via sentinel.
    /// </summary>
    public string ServiceName { get; set; } = "mymaster";
}

public enum ERedisConnectionType
{
    Normal,
    Sentinel,
    Cluster
}