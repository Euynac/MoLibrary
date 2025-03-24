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

                //����sentinel
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
                //redisOptions.AllowAdmin = true; //����ִ�й���Ա����
                //redisOptions.Password = "P@ssw0rd123";
                //redisOptions.AbortOnConnectFail = true; //�������ʧ�����׳��쳣
                //redisOptions.ConnectTimeout = 5000; //(����) ���ӳ�ʱʱ��
                //redisOptions.SyncTimeout = 5000; //ͬ��������ʱʱ��
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
                    //�Զ�����ʽ���ű����룬������ΪΪǶ��ʽ��Դ
                    configuration.UseDashboardStylesheetDarkMode(typeof(GlobalConfigurationExtension).Assembly, "Hangfire.HttpJob.Content.job.css");
                    configuration.UseDashboardJavaScript(typeof(GlobalConfigurationExtension).Assembly, "Hangfire.HttpJob.Content.job.js");

                    configuration.UseFilter(new AutomaticRetryAttribute() { Attempts = 0 });
                    configuration.UseHangfireHttpJob(new HangfireHttpJobOptions()
                    {
                        UseEmail = false, //�Ƿ�ʹ������
                        AutomaticDelete = 2, //������ҵִ�ж�ù��ڣ���λ�죬Ĭ��2��
                        DeleteOnFail = true,
                        AttemptsCountArray = [5],// �������� ����ʱ���������鳤�������Դ���
                                                 //AddHttpJobButtonName = "add plan job",
                                                 //AddRecurringJobHttpJobButtonName = "add httpjob",
                                                 //EditRecurringJobButtonName = "edit httpjob",
                        PauseJobButtonName = "��ͣ�����",
                        UpdateCronButtonName = "�޸�����",
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
                    InvisibilityTimeout = TimeSpan.FromHours(1), //���ʱʱ��
                    ExpiryCheckInterval = TimeSpan.FromHours(1), //������ڼ��Ƶ��
                    DeletedListSize = 10000,
                    SucceededListSize = 10000
                });
            }
            catch (Exception ex)
            {
                GlobalLog.LogError(ex, "SetHangfire����");
                _hasError = true;
            }
        }
        else
        {
            throw new InvalidOperationException("Hangfireδѡ��ʹ��Redis��InMemory����ģʽ");
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
            op.SchedulePollingInterval = TimeSpan.FromSeconds(1); //�뼶������Ҫ���ö̵㣬һ�������������Ĭ��ʱ�䣬Ĭ��15��
            op.ShutdownTimeout = TimeSpan.FromMinutes(30); //��ʱʱ��
            op.Queues = _options.Queues.ToArray();
            op.WorkerCount = Math.Max(Environment.ProcessorCount, 40); //�����߳�������ǰ���������̣߳�Ĭ��20
            op.StopTimeout = TimeSpan.FromSeconds(20);
        });
        //, JobStorage.Current, new[] { new ProcessMonitor(checkInterval: TimeSpan.FromSeconds(1)) }
    }


    public static void UseMoBackgroundWorker(this IApplicationBuilder app)
    {
        if (_backgroundWorkerTypes.Count <= 0 || _hasError) return;
        GlobalLog.LogInformation("���ּ̳���OurBackgroundWorker��Job���࣬������Hangfire��̨�������");

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

        //��¼�������
        app.UseHangfireDashboard("/worker", new DashboardOptions()
        {
            AppPath = "#",
            DisplayStorageConnectionString = _options.DisplayStorageConnectionString,  //�Ƿ���ʾ���ݿ�������Ϣ
            DashboardTitle = _options.DashboardTitle,
            IsReadOnlyFunc = context =>
            {
                var isReadonly = false;
                return isReadonly;
            },
            Authorization = new[] { new MoHangfireAuthorizationFilter() }
        });

        //�Զ�ע���̨Worker
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
                GlobalLog.LogInformation("��ע��򵥺�̨����{workerType}", workerType);
            }
            else
            {
                backgroundWorkerManager.AddToDashboardAsync(workerType, _options.Queues.First());//TODO �����ȡ
                GlobalLog.LogInformation("��ע������̨����{workerType}", workerType);
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