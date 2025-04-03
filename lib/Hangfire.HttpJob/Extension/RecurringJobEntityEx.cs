using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Cronos;

namespace Hangfire.HttpJob.Extension
{
    public class RecurringJobEntityEx
    {
        public static bool TryGetNextExecution(
            string cron,
            DateTime? lastExecution,
            DateTime createdAt,
            DateTime? from,
            TimeZoneInfo timezone,
            out DateTime? nextExecution,
            out Exception exception)
        {
            try
            {
                nextExecution = (DateTime?) ParseCronExpression(cron)?.GetNextOccurrence(from ?? lastExecution ?? createdAt.AddSeconds(-1.0), timezone);
                exception = (Exception) null;
                return true;
            }
            catch (Exception ex)
            {
                exception = ex;
                nextExecution = new DateTime?();
                return false;
            }
        }

        public static CronExpression ParseCronExpression(string cronExpression)
        {
            string[] strArray = cronExpression != null ? cronExpression.Split(new char[2]
            {
                ' ',
                '\t'
            }, StringSplitOptions.RemoveEmptyEntries) : throw new ArgumentNullException(nameof(cronExpression));
            CronFormat format = CronFormat.Standard;
            if (strArray.Length == 6)
                format |= CronFormat.IncludeSeconds;
            else if (strArray.Length != 5)
                throw new CronFormatException("Wrong number of parts in the `" + cronExpression + "` cron expression, you can only use 5 or 6 (with seconds) part-based expressions.");
            return CronExpression.Parse(cronExpression, format);
        }
    }
}
