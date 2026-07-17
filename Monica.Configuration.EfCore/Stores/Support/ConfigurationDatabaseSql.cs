namespace Monica.Configuration.EfCore.Stores.Support;

internal static class ConfigurationDatabaseSql
{
    internal static bool IsSqlServer(string? providerName)
    {
        return providerName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) is true;
    }

    internal static bool IsMySql(string? providerName)
    {
        return providerName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) is true;
    }

    internal static string FormatTableName(string? providerName, string? schema, string tableName)
    {
        if (IsSqlServer(providerName))
        {
            return string.IsNullOrWhiteSpace(schema)
                ? QuoteSqlServer(tableName)
                : $"{QuoteSqlServer(schema)}.{QuoteSqlServer(tableName)}";
        }

        if (IsMySql(providerName))
        {
            return string.IsNullOrWhiteSpace(schema)
                ? QuoteMySql(tableName)
                : $"{QuoteMySql(schema)}.{QuoteMySql(tableName)}";
        }

        return string.IsNullOrWhiteSpace(schema)
            ? QuoteAnsi(tableName)
            : $"{QuoteAnsi(schema)}.{QuoteAnsi(tableName)}";
    }

    internal static string QuoteIdentifier(string? providerName, string identifier)
    {
        if (IsSqlServer(providerName))
        {
            return QuoteSqlServer(identifier);
        }

        if (IsMySql(providerName))
        {
            return QuoteMySql(identifier);
        }

        return QuoteAnsi(identifier);
    }

    private static string QuoteAnsi(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    internal static string BuildStoreLockUpdate(string? providerName)
    {
        var tableSql = FormatTableName(providerName, null, "ConfigurationSchemaMarkers");
        var markerKeySql = QuoteIdentifier(providerName, "MarkerKey");
        var schemaVersionSql = QuoteIdentifier(providerName, "SchemaVersion");
        return $"UPDATE {tableSql} SET {schemaVersionSql} = {schemaVersionSql} WHERE {markerKeySql} = {{0}}";
    }

    private static string QuoteSqlServer(string identifier)
    {
        return $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
    }

    private static string QuoteMySql(string identifier)
    {
        return $"`{identifier.Replace("`", "``", StringComparison.Ordinal)}`";
    }
}
