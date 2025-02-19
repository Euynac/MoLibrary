
using Hangfire.Common;
using Hangfire.Dashboard;
using Hangfire.Logging;
using Hangfire.Storage;
using Newtonsoft.Json;
using System.Net;
using Hangfire.HttpJob.Extension;
using Microsoft.VisualBasic;

namespace Hangfire.HttpJob.Server
{
    public class HttpJobDispatcher : IDashboardDispatcher
    {
        private readonly ILog _logger = LogProvider.For<HttpJobDispatcher>();
        private const string PAUSE_CRON_EXPRESSION = "0 0 0 1 1 ?";
 
        public HttpJobDispatcher(HangfireHttpJobOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
        }

        public Task Dispatch(DashboardContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            try
            {
                if (!"POST".Equals(context.Request.Method, StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    return Task.FromResult(false);
                }

                var op = context.Request.GetQuery("op");
                if (string.IsNullOrEmpty(op))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    return Task.FromResult(false);
                }
                if (op.ToLower() == "getjoblist")
                {
                    var joblist = GetRecurringJobs();
                    context.Response.ContentType = "application/json";
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.WriteAsync(JsonConvert.SerializeObject(joblist));
                    return Task.FromResult(true);
                }
                var jobItem = GetJobItem(context);
                if (op.ToLower() == "getrecurringjob")
                {
                    var strdata = GetJobdata(jobItem.JobName);
                    if (!string.IsNullOrEmpty(strdata))
                    {
                        context.Response.ContentType = "application/json";
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.WriteAsync(strdata);
                        return Task.FromResult(true);
                    }
                    else
                    {
                        context.Response.ContentType = "application/json";
                        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        return Task.FromResult(false);
                    }
                }
                if (jobItem == null || string.IsNullOrEmpty(jobItem.Url) || string.IsNullOrEmpty(jobItem.ContentType))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    return Task.FromResult(false);
                }

                if (string.IsNullOrEmpty(jobItem.JobName))
                {
                    var jobName = context.Request.Path.Split('/').LastOrDefault() ?? string.Empty;
                    jobItem.JobName = jobName;
                }

                if (string.IsNullOrEmpty(jobItem.JobName))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    return Task.FromResult(false);
                }

                var result = false;
                switch (op.ToLower())
                {
                    case "backgroundjob":
                        result = AddHttpbackgroundjob(jobItem);
                        break;

                    case "recurringjob":
                        if (string.IsNullOrEmpty(jobItem.Cron))
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                            return Task.FromResult(false);
                        }
                        result = AddHttprecurringjob(jobItem);
                        break;

                    case "editrecurringjob":
                        if (string.IsNullOrEmpty(jobItem.Cron))
                        {
                            context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                            return Task.FromResult(false);
                        }
                        result = AddHttprecurringjob(jobItem);
                        break;

                    case "pausejob":
                        //result = PauseOrRestartJob(jobItem.JobName);
                        result = PauseOrRestartRecurringJob(jobItem.JobName);
                        break;

                    case "updatecron":
                        //result = PauseOrRestartJob(jobItem.JobName);
                        result = UpdateCron(jobItem);
                        break;

                    default:
                        context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                        return Task.FromResult(false);
                }

                if (result)
                {
                    context.Response.ContentType = "application/json";
                    context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                    return Task.FromResult(true);
                }
                else
                {
                    context.Response.ContentType = "application/json";
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    return Task.FromResult(false);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"HttpJobDispatcher.Dispatch:{ex.StackTrace}");
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return Task.FromResult(false);
            }
        }

        public HttpJobItem GetJobItem(DashboardContext _context)
        {
            try
            {
                var context = _context.GetHttpContext();
                using (MemoryStream ms = new MemoryStream())
                {
                    context.Request.Body.CopyTo(ms);
                    ms.Flush();
                    ms.Seek(0, SeekOrigin.Begin);
                    var sr = new StreamReader(ms);
                    var requestBody = sr.ReadToEnd();
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<HttpJobItem>(requestBody);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"HttpJobDispatcher.GetJobItem: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// 获取job任务
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string GetJobdata(string name)
        {
            try
            {
                using (var connection = JobStorage.Current.GetConnection())
                {
                    var RecurringJob = StorageConnectionExtensions.GetRecurringJobs(connection).
                        Where(p => p.Id == name).FirstOrDefault();
                    if (RecurringJob != null)
                    {
                        return JsonConvert.SerializeObject(
                            JsonConvert.DeserializeObject<RecurringJobItem>(RecurringJob.Job.Args.FirstOrDefault().ToString())
                            );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"获取job失败： 错误消息 {ex.Message}\n 堆栈信息： {ex.StackTrace}");
                throw;
            }

            return "";
        }

        /// <summary>
        /// 添加后台作业
        /// </summary>
        /// <param name="jobItem"></param>
        /// <returns></returns>
        public bool AddHttpbackgroundjob(HttpJobItem jobItem)
        {
            try
            {
                BackgroundJob.Schedule<HttpJob>(a => a.ExcuteAsync(jobItem, jobItem.JobName, jobItem.QueueName, jobItem.IsRetry, null), TimeSpan.FromMinutes(jobItem.DelayFromMinutes));
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"HttpJobDispatcher.AddHttpbackgroundjob: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 暂停或者开始任务
        /// </summary>
        /// <param name="jobname"></param>
        /// <returns></returns>
        public bool PauseOrRestartJob(string jobname)
        {
            try
            {
                using (var connection = JobStorage.Current.GetConnection())
                {
                    using (var tran = connection.CreateWriteTransaction())
                    {
                        var conts = connection.GetAllItemsFromSet($"JobPauseOf:{jobname}");
                        if (conts.Contains("true"))
                        {
                            tran.RemoveFromSet($"JobPauseOf:{jobname}", "true");
                            tran.AddToSet($"JobPauseOf:{jobname}", "false");
                            tran.Commit();
                        }
                        else
                        {
                            tran.RemoveFromSet($"JobPauseOf:{jobname}", "false");
                            tran.AddToSet($"JobPauseOf:{jobname}", "true");
                            tran.Commit();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"暂停job失败： 错误消息 {ex.Message}\n 堆栈信息： {ex.StackTrace}");
                throw;
            }
            return true;
        }

        /// <summary>
        /// 获取已经暂停的任务
        /// </summary>
        /// <returns></returns>
        public List<PauseRecurringJob> GetRecurringJobs()
        {
            var pauselist = new List<PauseRecurringJob>();
            using (var connection = JobStorage.Current.GetConnection())
            {
                var joblist = StorageConnectionExtensions.GetRecurringJobs(connection);
                joblist.ForEach(k =>
                {
                    var conts = connection.GetAllItemsFromSet($"JobPauseOf:{k.Id}");
                    if (conts.Contains("true"))
                    {
                        var pauseinfo = new PauseRecurringJob() { Id = k.Id };
                        pauselist.Add(pauseinfo);
                    }
                });
            }
            return pauselist;
        }

        /// <summary>
        /// 添加周期性作业
        /// </summary>
        /// <param name="jobItem"></param>
        /// <returns></returns>
        public bool AddHttprecurringjob(HttpJobItem jobItem)
        {
            //get queues from server
            var server = JobStorage.Current.GetMonitoringApi().Servers().
                Where(p => p.Queues.Count > 0).FirstOrDefault();
            var queues = server.Queues.ToList();
            if (!queues.Exists(p => p == jobItem.QueueName.ToLower()) || queues.Count == 0)
            {
                return false;
            }
            try
            {
                //RecurringJob.AddOrUpdate<HttpJob>(jobItem.JobName, jobItem.QueueName, a => a.ExcuteAsync(jobItem, jobItem.JobName, jobItem.QueueName, jobItem.IsRetry, null), jobItem.Corn);
                RecurringJob.AddOrUpdate<HttpJob>(jobItem.JobName, a => a.ExcuteAsync(jobItem, jobItem.JobName, jobItem.QueueName, jobItem.IsRetry, null), jobItem.Cron, new RecurringJobOptions()
                {
                    QueueName = jobItem.QueueName,
                    TimeZone = TimeZoneInfo.Local
                });
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"HttpJobDispatcher.AddHttprecurringjob: {ex.StackTrace}");
                return false;
            }
        }

        public bool PauseOrRestartRecurringJob(string jobName)
        {
            try
            {
                var job = GetRecurringJob(jobName);
                if (job != null)
                {
                    bool isRestart = false;
                    string origCron = "";
                    if (IsPause(job.Cron))
                    {
                        isRestart = true;
                    }
                    else
                    {
                        origCron = job.Cron;
                        job.Cron = PAUSE_CRON_EXPRESSION;
                    }

                    using var connection = JobStorage.Current.GetConnection();
                    using (connection.AcquireDistributedLock($"lock:recurring-job:{jobName}", TimeSpan.FromSeconds(15)))
                    {
                        var jobItems = connection.GetAllEntriesFromHash($"recurring-job:{jobName}");
                        using (var tran = connection.CreateWriteTransaction())
                        {
                            DateTime? nextExecution = null;
                            if (isRestart)
                            {
                                job.Cron = jobItems["OrigCron"];
                                RecurringJobEntityEx.TryGetNextExecution(job.Cron, job.LastExecution, job.CreatedAt!.Value,
                                    DateTime.UtcNow, (job.TimeZoneId == "UTC" ? TimeZoneInfo.Utc : TimeZoneInfo.Local), out nextExecution, out var ex);
                                double score = nextExecution.HasValue ? (double) JobHelper.ToTimestamp(nextExecution.Value) : -1.0;
                                tran.AddToSet("recurring-jobs", jobName, score);
                            }
                            else
                            {
                                jobItems["OrigCron"] = origCron;
                                //tran.RemoveFromSet("recurring-jobs", jobName);
                            }
                            jobItems["Cron"] = job.Cron;
                            if (nextExecution.HasValue)
                            {
                                jobItems["NextExecution"] = nextExecution.Value.ToString("s");
                            }

                            tran.SetRangeInHash($"recurring-job:{jobName}", jobItems);
                            tran.Commit();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"暂停Recurring Job失败： 错误消息 {ex.Message}\n 堆栈信息： {ex.StackTrace}");
                throw;
            }
            return true;
        }

        public bool UpdateCron(HttpJobItem jobItem)
        {
            try
            {
                try
                {
                    RecurringJobEntityEx.ParseCronExpression(jobItem.Cron);
                }
                catch (Exception e)
                {
                    throw new Exception("周期格式不对");
                }

                var job = GetRecurringJob(jobItem.JobName);
                if (job != null)
                {
                    using var connection = JobStorage.Current.GetConnection();
                    using (connection.AcquireDistributedLock($"lock:recurring-job:{jobItem.JobName}", TimeSpan.FromSeconds(15)))
                    {
                        var jobItems = connection.GetAllEntriesFromHash($"recurring-job:{jobItem.JobName}");
                        using (var tran = connection.CreateWriteTransaction())
                        {
                            job.Cron = jobItem.Cron;
                            RecurringJobEntityEx.TryGetNextExecution(job.Cron, job.LastExecution, job.CreatedAt!.Value,
                                DateTime.UtcNow, (job.TimeZoneId == "UTC" ? TimeZoneInfo.Utc : TimeZoneInfo.Local), out var nextExecution, out var ex);
                            double score = nextExecution.HasValue ? (double) JobHelper.ToTimestamp(nextExecution.Value) : -1.0;
                            tran.AddToSet("recurring-jobs", jobItem.JobName, score);
                            jobItems["Cron"] = job.Cron;
                            if (nextExecution.HasValue)
                            {
                                jobItems["NextExecution"] = nextExecution.Value.ToString("s");
                            }

                            tran.SetRangeInHash($"recurring-job:{jobItem.JobName}", jobItems);
                            tran.Commit();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"修改Recurring Job周期失败： 错误消息 {ex.Message}\n 堆栈信息： {ex.StackTrace}");
                throw;
            }
            return true;
        }


        private RecurringJobDto? GetRecurringJob(string jobName)
        {
            try
            {
                using var connection = JobStorage.Current.GetConnection();
                var job = connection.GetRecurringJobs().FirstOrDefault(p => p.Id == jobName);
                return job;
            }
            catch (Exception ex)
            {
                _logger.Error($"获取job失败： 错误消息 {ex.Message}\n 堆栈信息： {ex.StackTrace}");
                throw;
            }
        }

        private bool IsPause(string cron)
        {
            return cron == PAUSE_CRON_EXPRESSION;
        }
    }
}