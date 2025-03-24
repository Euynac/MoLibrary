using System.Reflection;
using BuildingBlocksPlatform.BackgroundWorker.Abstract.Jobs;
using BuildingBlocksPlatform.BackgroundWorker.Abstract.Workers;
using BuildingBlocksPlatform.BackgroundWorker.Attributes;
using BuildingBlocksPlatform.BackgroundWorker.Hangfire.Jobs;
using BuildingBlocksPlatform.BackgroundWorker.Hangfire.Workers;
using BuildingBlocksPlatform.BackgroundWorker.MoTaskScheduler;
using BuildingBlocksPlatform.Interfaces;
using BuildingBlocksPlatform.SeedWork;
using Hangfire;
using Hangfire.Common;
using Hangfire.Console;
using Hangfire.Dashboard;
using Hangfire.Heartbeat;
using Hangfire.HttpJob;
using Hangfire.Redis;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Features;
using StackExchange.Redis;

namespace BuildingBlocksPlatform.BackgroundWorker;

public static class ServiceCollectionExtension
{
    private static List<Type> _backgroundWorkerTypes = [];
    private static bool _hasError = false;
    private static readonly MoBackgroundWorkerOptions _options = new ();
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
        else if(_options.RedisOptions is {} redisConfig)
        {
            try
            {

                var redisOptions = new ConfigurationOptions();

                //配置sentinel
                var sentinelOptions = new ConfigurationOptions();
                sentinelOptions.EndPoints.Add(redisConfig.RedisHost, redisConfig.RedisPort);
                sentinelOptions.TieBreaker = "";
                sentinelOptions.Password = redisConfig.RedisPassword;
                sentinelOptions.CommandMap = CommandMap.Sentinel;
                sentinelOptions.AbortOnConnectFail = false;
                var sentinelConnection = ConnectionMultiplexer.Connect(sentinelOptions);

                redisOptions.ServiceName = "mymaster";
                redisOptions.Password = redisConfig.RedisPassword;
                redisOptions.AbortOnConnectFail = true;
                var myMasterConnection = sentinelConnection.GetSentinelMasterConnection(redisOptions);


                //var redisOptions = new ConfigurationOptions();
                //redisOptions.EndPoints.Add("dev-redis-cluster-headless.fips-dev",6379);
                //redisOptions.AllowAdmin = true; //允许执行管理员命令
                //redisOptions.Password = "P@ssw0rd123";
                //redisOptions.AbortOnConnectFail = true; //如果连接失败则抛出异常
                //redisOptions.ConnectTimeout = 5000; //(毫秒) 连接超时时间
                //redisOptions.SyncTimeout = 5000; //同步操作超时时间
                //var myMasterConnection = ConnectionMultiplexer.Connect(redisOptions);
                // redisOptions.EndPoints.Add(config.AppOptions.RedisOptions.RedisHost,
                //     config.AppOptions.RedisOptions.RedisPort);
                //// redisOptions.ServiceName = "mymaster";
                // redisOptions.AbortOnConnectFail = true;
                // redisOptions.Password = config.AppOptions.RedisOptions.RedisPassword;
                // myMasterConnection = ConnectionMultiplexer.Connect(redisOptions);
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

                JobStorage.Current = new RedisStorage(myMasterConnection, new RedisStorageOptions()
                {
                    Db = 10,
                    FetchTimeout = TimeSpan.FromSeconds(5),
                    Prefix = "{IMFHangfire}:",
                    InvisibilityTimeout = TimeSpan.FromHours(1), //活动超时时间
                    ExpiryCheckInterval = TimeSpan.FromHours(1), //任务过期检查频率
                    DeletedListSize = 10000,
                    SucceededListSize = 10000
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

        var candidates = Assembly.GetEntryAssembly()!.GetRelatedAssemblies(_options.RelatedAssemblies);
        _backgroundWorkerTypes =
            candidates.SelectMany(p => p.GetTypes()).Where(t =>
                    t.IsAssignableTo(typeof(IMoBackgroundWorker)))
                .ToList();

        if (_backgroundWorkerTypes.Count <= 0) return;


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


        services.AddTransient<IBackgroundJobManager, HangfireBackgroundJobManager>();
        services.AddTransient<IBackgroundJobExecutor, BackgroundJobExecutor>();

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
    }
}

file class MoHangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        return true;
    }
}