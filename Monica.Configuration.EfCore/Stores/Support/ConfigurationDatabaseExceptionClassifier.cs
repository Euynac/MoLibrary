using System.Data.Common;

namespace Monica.Configuration.EfCore.Stores.Support;

internal static class ConfigurationDatabaseExceptionClassifier
{
    internal static bool IsMissingSchemaObject(Exception exception)
    {
        return ContainsDatabaseException(exception, static databaseException =>
            GetStringProperty(databaseException, "SqlState") is "42P01"
            || GetStringProperty(databaseException, "SqlState") is "42703"
            || GetIntProperty(databaseException, "Number") is 208 or 1146
            || GetIntProperty(databaseException, "Number") is 207 or 1054
            || GetIntProperty(databaseException, "SqliteErrorCode") is 1
               && (databaseException.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase)
                   || databaseException.Message.Contains("no such column", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool ContainsDatabaseException(
        Exception exception,
        Func<DbException, bool> predicate)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbException databaseException && predicate(databaseException))
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetStringProperty(object instance, string propertyName)
    {
        return instance.GetType().GetProperty(propertyName)?.GetValue(instance) as string;
    }

    private static int? GetIntProperty(object instance, string propertyName)
    {
        return instance.GetType().GetProperty(propertyName)?.GetValue(instance) as int?;
    }
}
