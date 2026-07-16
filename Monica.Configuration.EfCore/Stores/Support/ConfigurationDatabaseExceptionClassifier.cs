using System.Data.Common;

namespace Monica.Configuration.EfCore.Stores.Support;

internal static class ConfigurationDatabaseExceptionClassifier
{
    internal static bool IsMissingTable(Exception exception)
    {
        return ContainsDatabaseException(exception, static databaseException =>
            GetStringProperty(databaseException, "SqlState") is "42P01"
            || GetIntProperty(databaseException, "Number") is 208 or 1146
            || GetIntProperty(databaseException, "SqliteErrorCode") is 1
               && databaseException.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsMissingColumn(Exception exception)
    {
        return ContainsDatabaseException(exception, static databaseException =>
            GetStringProperty(databaseException, "SqlState") is "42703"
            || GetIntProperty(databaseException, "Number") is 207 or 1054
            || GetIntProperty(databaseException, "SqliteErrorCode") is 1
               && databaseException.Message.Contains("no such column", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsDuplicateColumn(Exception exception)
    {
        return ContainsDatabaseException(exception, static databaseException =>
            GetStringProperty(databaseException, "SqlState") is "42701"
            || GetIntProperty(databaseException, "Number") is 2705 or 1060
            || GetIntProperty(databaseException, "SqliteErrorCode") is 1
               && databaseException.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsDuplicateIndex(Exception exception)
    {
        return ContainsDatabaseException(exception, static databaseException =>
            GetStringProperty(databaseException, "SqlState") is "42P07"
            || GetIntProperty(databaseException, "Number") is 1913 or 2714 or 1061
            || GetIntProperty(databaseException, "SqliteErrorCode") is 1
               && databaseException.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsMissingIndex(Exception exception)
    {
        return ContainsDatabaseException(exception, static databaseException =>
            GetIntProperty(databaseException, "Number") is 1091
            || GetStringProperty(databaseException, "SqlState") is "42704"
            || GetIntProperty(databaseException, "SqliteErrorCode") is 1
               && databaseException.Message.Contains("no such index", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsUniqueConstraintViolation(Exception exception)
    {
        return ContainsDatabaseException(exception, static databaseException =>
            GetStringProperty(databaseException, "SqlState") is "23505"
            || GetIntProperty(databaseException, "Number") is 2601 or 2627 or 1062
            || GetIntProperty(databaseException, "SqliteErrorCode") is 19);
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
