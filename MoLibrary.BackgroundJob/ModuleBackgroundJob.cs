using Hangfire.Redis;
using Hangfire;
using Hangfire.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.BackgroundJob.Abstract.Jobs;
using MoLibrary.BackgroundJob.Abstract.Workers;
using MoLibrary.BackgroundJob.Attributes;
using MoLibrary.BackgroundJob.Hangfire.Jobs;
using MoLibrary.BackgroundJob.Hangfire.Workers;
using MoLibrary.BackgroundJob.MoTaskScheduler;
using MoLibrary.Core.Module;
using MoLibrary.Tool.MoResponse;
using BuildingBlocksPlatform.Interfaces;
using MoLibrary.Tool.Extensions;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace MoLibrary.BackgroundJob;

public static class ModuleBuilderExtensionsAuthorization
{
    public static ModuleGuideBackgroundJob AddMoModuleBackgroundJob(this IServiceCollection services)
    {
        return MoModule.Register<ModuleBackgroundJob, MoBackgroundWorkerOptions, ModuleGuideBackgroundJob>();
    }
}

public class ModuleBackgroundJob(MoBackgroundWorkerOptions option) : MoModule<ModuleBackgroundJob, MoBackgroundWorkerOptions, ModuleGuideBackgroundJob>(option), IWantIterateBusinessTypes
{
    private bool _hasError = false;
    private readonly List<Type> _backgroundWorkerTypes = [];
    private readonly List<Type> _backgroundJobTypes = [];
    public override EMoModules GetMoModuleEnum()
    {
        return EMoModules.BackgroundJob;
    }

    public override Res PostConfigureServices(IServiceCollection services)
    {
        if (_backgroundWorkerTypes.Count <= 0 && _backgroundJobTypes.Count <= 0) return "不存在需要自动注册的Worker或Jobs";
        HangfireRedisGlobalOptions.Queues = Option.Queues;
        HangfireRedisGlobalOptions.SupportedProject = Option.SupportedProject;
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
        _hasError = ServiceCollectionExtensions.SetHangfire(services);
        if (_hasError) return "设置Hangfire出现异常";
        services.AddHangfireServer((provider, op) =>
        {
            op.ServerTimeout = TimeSpan.FromMinutes(4);
            op.SchedulePollingInterval = TimeSpan.FromSeconds(1); //秒级任务需要配置短点，一般任务可以配置默认时间，默认15秒
            op.ShutdownTimeout = TimeSpan.FromMinutes(30); //超时时间
            op.Queues = Option.Queues.ToArray();
            op.WorkerCount = Math.Max(Environment.ProcessorCount, 40); //工作线程数，当前允许的最大线程，默认20
            op.StopTimeout = TimeSpan.FromSeconds(20);
        });
        //, JobStorage.Current, new[] { new ProcessMonitor(checkInterval: TimeSpan.FromSeconds(1)) }

        return Res.Ok();
    }

    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            if (type.IsAssignableTo(typeof(IMoBackgroundWorker)))
            {
                _backgroundWorkerTypes.Add(type);
            }
            if (type.IsAssignableTo(typeof(IMoBackgroundJob<>)))
            {
                _backgroundJobTypes.Add(type);
            }
            yield return type;
        }
    }

    public override Res UseMiddlewares(IApplicationBuilder app)
    {
        Logger.LogInformation("发现继承自BackgroundWorker或Job的类，将启用Hangfire后台任务调度");

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
            DisplayStorageConnectionString = Option.DisplayStorageConnectionString,  //是否显示数据库连接信息
            DashboardTitle = Option.DashboardTitle,
            IsReadOnlyFunc = context =>
            {
                var isReadonly = false;
                return isReadonly;
            },
            Authorization = new[] { new MoHangfireAuthorizationFilter() }
        });

        if (Option.DisableAutoRegister)
        {
            Logger.LogInformation("已禁用Worker自动注册");
            return Res.Ok();
        }

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
                Logger.LogInformation("已注册简单后台任务：{workerType}", workerType);
            }
            else
            {
                backgroundWorkerManager.AddToDashboardAsync(workerType, Option.Queues.First());//TODO 合理读取
                Logger.LogInformation("已注册面板后台任务：{workerType}", workerType);
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
            Logger.LogInformation("已注册后台任务：{jobType}", jobType);
        }
        return Res.Ok();
    }
}

public class ModuleGuideBackgroundJob : MoModuleGuide<ModuleBackgroundJob>
{


}