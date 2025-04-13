using System.Reflection;
using BuildingBlocksPlatform.Interfaces;
using Hangfire;
using Hangfire.Common;
using Hangfire.Console;
using Hangfire.Dashboard;
using Hangfire.Heartbeat;
using Hangfire.HttpJob;
using Hangfire.Redis;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.BackgroundJob.Abstract.Jobs;
using MoLibrary.BackgroundJob.Abstract.Workers;
using MoLibrary.BackgroundJob.Attributes;
using MoLibrary.BackgroundJob.Hangfire.Jobs;
using MoLibrary.BackgroundJob.Hangfire.Workers;
using MoLibrary.BackgroundJob.MoTaskScheduler;
using MoLibrary.Core.Features;
using MoLibrary.Logging;
using MoLibrary.Tool.Extensions;
using StackExchange.Redis;

namespace MoLibrary.BackgroundJob;

public static class ServiceCollectionExtensions
{
    private static List<Type> _backgroundWorkerTypes = [];
    private static List<Type> _backgroundJobTypes = [];
    private static bool _hasError = false;
    private static readonly MoBackgroundWorkerOptions _options = new();
    private static void SetHangfire(IServiceCollection services)
    {
        if (_options.UseInMemoryStorage)
        {
            services.AddHangfire(configuration =>
            {
                configuration.UseInMemoryStorage();
                configuration.UseFilter(new AutomaticRetryAttribute() { Attempts = 0 });
            });
        }
        else if (_options.RedisOptions is { } redisConfig)
        {
            try
            {
                IConnectionMultiplexer redisConnection;

                switch (redisConfig.ConnectionType)
                {
                    case ERedisConnectionType.Sentinel:
                        {
                            //配置sentinel
                            var sentinelOptions = new ConfigurationOptions();
                            sentinelOptions.EndPoints.Add(redisConfig.RedisHost, redisConfig.RedisPort);
                            sentinelOptions.TieBreaker = "";
                            sentinelOptions.Password = redisConfig.RedisPassword;
                            sentinelOptions.CommandMap = CommandMap.Sentinel;
                            sentinelOptions.AbortOnConnectFail = false;
                            var sentinelConnection = ConnectionMultiplexer.Connect(sentinelOptions);
                            redisConnection = sentinelConnection.GetSentinelMasterConnection(new ConfigurationOptions
                            {
                                ServiceName = redisConfig.ServiceName,
                                Password = redisConfig.RedisPassword,
                                AbortOnConnectFail = true
                            });
                            break;
                        }
                    case ERedisConnectionType.Cluster:
                        {
                            var redisOptions = new ConfigurationOptions();

                            // 添加集群节点（至少一个节点即可，客户端会自动发现其他节点）
                            redisOptions.EndPoints.Add(redisConfig.RedisHost, redisConfig.RedisPort);  // 主节点或从节点
                                                                                                       //redisOptions.EndPoints.Add("redis-cluster-node2", 6379);  // 可选，增加容错性
                                                                                                       //redisOptions.EndPoints.Add("redis-cluster-node3", 6379);  // 可选

                            // 集群模式关键配置
                            redisOptions.Password = redisOptions.Password;  // 如果集群有密码
                            redisOptions.AbortOnConnectFail = false;        // 集群节点可能动态变化，建议不立即失败
                            redisOptions.ConnectRetry = 3;                  // 连接失败时重试次数
                            redisOptions.ConnectTimeout = 5000;             // 连接超时（毫秒）
                            redisOptions.SyncTimeout = 5000;                // 同步操作超时时间（毫秒）
                            redisOptions.AllowAdmin = true; //允许执行管理员命令

                            // 显式启用集群模式（StackExchange.Redis 会自动检测，但可以显式声明）
                            redisOptions.CommandMap = CommandMap.Default;    // 使用默认命令映射（支持集群）

                            // 创建集群连接
                            redisConnection = ConnectionMultiplexer.Connect(redisOptions);
                            break;
                        }
                    case ERedisConnectionType.Normal:
                    default:
                        {
                            var redisOptions = new ConfigurationOptions();
                            redisOptions.EndPoints.Add(redisConfig.RedisHost, redisConfig.RedisPort);
                            redisOptions.Password = redisConfig.RedisPassword;
                            redisOptions.AbortOnConnectFail = true;
                            redisConnection = ConnectionMultiplexer.Connect(redisOptions);
                            break;
                        }
                }




                JobStorage.Current = new RedisStorage(redisConnection, new RedisStorageOptions()
                {
                    Db = 10,
                    FetchTimeout = TimeSpan.FromSeconds(5),
                    Prefix = "{IMFHangfire}:",
                    InvisibilityTimeout = TimeSpan.MaxValue, //活动超时时间
                                                             //巨坑：该时间超时后会导致Hangfire认为该作业因各种原因挂掉，然后重新使用新线程去跑这个Job（旧线程它没去管，测了发现其实还在跑），默认值是30分钟，因此一些超过30分钟的Job可能会被执行多次。
                                                             //旧线程先跑也会在Dashboard面板上置为job完成，但实际上可能新线程还在跑。这也是Dashboard上为什么有些任务会有多个Process面板。


                    ExpiryCheckInterval = TimeSpan.MaxValue, //任务过期检查频率
                    DeletedListSize = 10000,
                    SucceededListSize = 10000
                });

                services.AddHangfire(configuration =>
                {
                    configuration.UseHeartbeatPage(checkInterval: TimeSpan.FromSeconds(1));
                    //自定义样式及脚本导入，必须设为为嵌入式资源
                    configuration.UseDashboardStylesheetDarkMode(typeof(GlobalConfigurationExtension).Assembly, "Hangfire.HttpJob.Content.job.css");
                    configuration.UseDashboardJavaScript(typeof(GlobalConfigurationExtension).Assembly, "Hangfire.HttpJob.Content.job.js");

                    configuration.UseFilter(new AutomaticRetryAttribute() { Attempts = 0 });
                    configuration.UseHangfireHttpJob(new HangfireHttpJobOptions()
                    {
                        UseEmail = false, //是否使用邮箱
                        AutomaticDelete = 2, //设置作业执行多久过期，单位天，默认2天
                        DeleteOnFail = true,
                        AttemptsCountArray = [5],// 重试配置 重试时间间隔，数组长度是重试次数
                                                 //AddHttpJobButtonName = "add plan job",
                                                 //AddRecurringJobHttpJobButtonName = "add httpjob",
                                                 //EditRecurringJobButtonName = "edit httpjob",
                        PauseJobButtonName = "暂停或继续",
                        UpdateCronButtonName = "修改周期",
                        DashboardName = _options.DashboardTitle,
                        DashboardFooter = _options.DashboardTitle
                    })
                    .UseConsole(new ConsoleOptions()
                    {
                        BackgroundColor = "#000000"
                    })
                    .UseDashboardMetrics(
                    [
                        DashboardMetrics.AwaitingCount,DashboardMetrics.ProcessingCount, DashboardMetrics.RecurringJobCount,
                   DashboardMetrics.RetriesCount,DashboardMetrics.FailedCount,DashboardMetrics.SucceededCount
                    ]);
                });


            }
            catch (Exception ex)
            {
                GlobalLog.LogError(ex, "SetHangfire报错");
                _hasError = true;
            }
        }
        else
        {
            throw new InvalidOperationException("Hangfire未选择使用Redis或InMemory连接模式");
        }
    }

    public static void AddMoBackgroundWorker(this IServiceCollection services, Action<MoBackgroundWorkerOptions>? action = null)
    {
        action?.Invoke(_options);

        var candidates = Assembly.GetEntryAssembly()!.GetRelatedAssemblies(_options.RelatedAssemblies).ToList();
        _backgroundWorkerTypes =
            candidates.SelectMany(p => p.GetTypes()).Where(t =>
                    t.IsAssignableTo(typeof(IMoBackgroundWorker)))
                .ToList();
        _backgroundJobTypes =
            candidates.SelectMany(p => p.GetTypes()).Where(t =>
                    t.IsAssignableTo(typeof(IMoBackgroundJob<>)))
                .ToList();
        if (_backgroundWorkerTypes.Count <= 0 && _backgroundJobTypes.Count <= 0) return;


        HangfireRedisGlobalOptions.Queues = _options.Queues;
        HangfireRedisGlobalOptions.SupportedProject = _options.SupportedProject;
        GlobalConfiguration.Configuration.UseTypeResolver(s =>
        {
            var test = TypeHelper.DefaultTypeResolver(s);
            return test;
        });
        GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute() { Attempts = 0 });
        GlobalJobFilters.Filters.Add(new SkipWhenPreviousRecurringJobIsRunningAttribute());
        GlobalJobFilters.Filters.Add(new SkipWhenPreviousJobIsRunningAttribute());


        services.AddSingleton<IMoDashboardBackgroundWorkerManager, MoHangfireBackgroundWorkerManager>();
        services.AddSingleton<IMoSimpleBackgroundWorkerManager, MoTaskSchedulerBackgroundWorkerManager>();
        services.AddSingleton<IMoBackgroundWorkerManager, MoBackgroundWorkerManager>();
        services.AddSingleton<IMoTaskScheduler, MoTaskSchedulerInMemoryProvider>();


        services.AddTransient<IMoBackgroundJobManager, HangfireBackgroundJobManager>();
        services.AddTransient<IBackgroundJobExecutor, DefaultBackgroundJobExecutor>();

        SetHangfire(services);
        if (_hasError) return;


        services.AddHangfireServer((provider, op) =>
        {
            op.ServerTimeout = TimeSpan.FromMinutes(4);
            op.SchedulePollingInterval = TimeSpan.FromSeconds(1); //秒级任务需要配置短点，一般任务可以配置默认时间，默认15秒
            op.ShutdownTimeout = TimeSpan.FromMinutes(30); //超时时间
            op.Queues = _options.Queues.ToArray();
            op.WorkerCount = Math.Max(Environment.ProcessorCount, 40); //工作线程数，当前允许的最大线程，默认20
            op.StopTimeout = TimeSpan.FromSeconds(20);
        });
        //, JobStorage.Current, new[] { new ProcessMonitor(checkInterval: TimeSpan.FromSeconds(1)) }
    }


    public static void UseMoBackgroundWorker(this IApplicationBuilder app)
    {
        if (_backgroundWorkerTypes.Count <= 0 || _hasError) return;
        GlobalLog.LogInformation("发现继承自OurBackgroundWorker或Job的类，将启用Hangfire后台任务调度");

        //var supportCulturs = new[]
        // {
        //     new CultureInfo ("zh-CN")
        // };
        //app.UseRequestLocalization(new RequestLocalizationOptions()
        //{
        //    DefaultRequestCulture = new RequestCulture("zh-CN"),
        //    SupportedCultures = supportCulturs,
        //    SupportedUICultures = supportCulturs
        //});

        //登录面板设置
        app.UseHangfireDashboard("/worker", new DashboardOptions()
        {
            AppPath = "#",
            DisplayStorageConnectionString = _options.DisplayStorageConnectionString,  //是否显示数据库连接信息
            DashboardTitle = _options.DashboardTitle,
            IsReadOnlyFunc = context =>
            {
                var isReadonly = false;
                return isReadonly;
            },
            Authorization = new[] { new MoHangfireAuthorizationFilter() }
        });

        //自动注册后台Worker
        var backgroundWorkerManager = app.ApplicationServices.GetRequiredService<IMoBackgroundWorkerManager>();

        foreach (var workerType in _backgroundWorkerTypes)
        {
            if (workerType.GetCustomAttribute<DisableAutoRegisterAttribute>() is not null) continue;
            if (!workerType.IsAssignableTo(typeof(IMoBackgroundWorker)))
            {
                throw new Exception($"Given type ({workerType.AssemblyQualifiedName}) must implement the {typeof(IMoBackgroundWorker).AssemblyQualifiedName} interface, but it doesn't!");
            }

            if (workerType.IsAssignableTo<IMoSimpleBackgroundWorker>())
            {
                backgroundWorkerManager.AddAsync(workerType);
                GlobalLog.LogInformation("已注册简单后台任务：{workerType}", workerType);
            }
            else
            {
                backgroundWorkerManager.AddToDashboardAsync(workerType, _options.Queues.First());//TODO 合理读取
                GlobalLog.LogInformation("已注册面板后台任务：{workerType}", workerType);
            }
        }

        //自动注册后台Job
        var backgroundJobManager = app.ApplicationServices.GetRequiredService<IMoBackgroundJobManager>();
        foreach (var jobType in _backgroundJobTypes)
        {
            if (jobType.GetCustomAttribute<DisableAutoRegisterAttribute>() is not null) continue;
            if (!jobType.IsAssignableTo(typeof(IMoBackgroundJob<>)))
            {
                throw new Exception($"Given type ({jobType.AssemblyQualifiedName}) must implement the {typeof(IMoBackgroundJob<>).AssemblyQualifiedName} interface, but it doesn't!");
            }
            backgroundJobManager.EnqueueAsync(jobType);
            GlobalLog.LogInformation("已注册后台任务：{jobType}", jobType);
        }
    }
}

file class MoHangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        return true;
    }
}