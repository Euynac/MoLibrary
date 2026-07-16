using Monica.Configuration.Models;

namespace Monica.Configuration.EfCore.Stores.Support;

internal static class ConfigurationDatabaseSql
{
    internal static string DefinitionIdentityColumnType =>
        $"char({ConfigurationDefinitionIdentity.Length})";

    internal static bool IsSqlServer(string? providerName)
    {
        return providerName?.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) is true;
    }

    internal static bool IsMySql(string? providerName)
    {
        return providerName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) is true;
    }

    internal static bool IsSqlite(string? providerName)
    {
        return providerName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) is true;
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

    internal static string QuoteAnsi(string identifier)
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

    internal static string BuildCreateTableIfMissing(
        string? providerName,
        string tableName,
        string tableSql,
        string columnsSql)
    {
        if (IsSqlServer(providerName))
        {
            return $"""
                    IF OBJECT_ID(N'{tableName.Replace("'", "''", StringComparison.Ordinal)}', N'U') IS NULL
                    BEGIN
                        CREATE TABLE {tableSql} (
                            {columnsSql}
                        );
                    END
                    """;
        }

        return $"""
                CREATE TABLE IF NOT EXISTS {tableSql} (
                    {columnsSql}
                );
                """;
    }

    internal static string BuildSchemaMarkerColumns(string? providerName)
    {
        return providerName switch
        {
            var name when IsSqlServer(name) =>
                "[MarkerKey] nvarchar(100) NOT NULL, [SchemaVersion] int NOT NULL, CONSTRAINT [PK_ConfigurationSchemaMarkers] PRIMARY KEY ([MarkerKey])",
            var name when IsMySql(name) =>
                "`MarkerKey` varchar(100) NOT NULL, `SchemaVersion` int NOT NULL, PRIMARY KEY (`MarkerKey`)",
            _ =>
                "\"MarkerKey\" varchar(100) NOT NULL, \"SchemaVersion\" integer NOT NULL, PRIMARY KEY (\"MarkerKey\")"
        };
    }

    internal static string BuildUnifiedVersionsColumns(string? providerName)
    {
        return providerName switch
        {
            var name when IsSqlServer(name) =>
                "[Version] bigint NOT NULL, [MutationGroupId] nvarchar(450) NULL, [TriggerDefinitionKeysJson] nvarchar(max) NOT NULL, [DefinitionKeysJson] nvarchar(max) NOT NULL, [DefinitionCount] int NOT NULL, [CreatedTime] datetime2(6) NOT NULL, [ModifierId] nvarchar(max) NULL, [ModifierName] nvarchar(max) NULL, [Reason] nvarchar(max) NULL, CONSTRAINT [PK_ConfigurationUnifiedVersions] PRIMARY KEY ([Version])",
            var name when IsMySql(name) =>
                "`Version` bigint NOT NULL, `MutationGroupId` varchar(191) NULL, `TriggerDefinitionKeysJson` longtext NOT NULL, `DefinitionKeysJson` longtext NOT NULL, `DefinitionCount` int NOT NULL, `CreatedTime` datetime(6) NOT NULL, `ModifierId` longtext NULL, `ModifierName` longtext NULL, `Reason` longtext NULL, PRIMARY KEY (`Version`)",
            var name when name?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) is true
                          || name?.Contains("GaussDB", StringComparison.OrdinalIgnoreCase) is true =>
                "\"Version\" bigint NOT NULL, \"MutationGroupId\" text NULL, \"TriggerDefinitionKeysJson\" text NOT NULL, \"DefinitionKeysJson\" text NOT NULL, \"DefinitionCount\" integer NOT NULL, \"CreatedTime\" timestamp with time zone NOT NULL, \"ModifierId\" text NULL, \"ModifierName\" text NULL, \"Reason\" text NULL, PRIMARY KEY (\"Version\")",
            _ =>
                "\"Version\" integer NOT NULL, \"MutationGroupId\" text NULL, \"TriggerDefinitionKeysJson\" text NOT NULL, \"DefinitionKeysJson\" text NOT NULL, \"DefinitionCount\" integer NOT NULL, \"CreatedTime\" timestamp NOT NULL, \"ModifierId\" text NULL, \"ModifierName\" text NULL, \"Reason\" text NULL, PRIMARY KEY (\"Version\")"
        };
    }

    internal static string BuildUnifiedVersionDocumentsColumns(string? providerName)
    {
        return providerName switch
        {
            var name when IsSqlServer(name) =>
                "[Version] bigint NOT NULL, [DefinitionKey] nvarchar(450) NOT NULL, [DefinitionIdentity] char(64) NOT NULL, [DisplayName] nvarchar(max) NOT NULL, [Category] nvarchar(max) NULL, [FromProject] nvarchar(max) NOT NULL, [SchemaVersion] int NOT NULL, [SchemaHash] nvarchar(max) NOT NULL, [EffectiveValueVersion] bigint NULL, [Json] nvarchar(max) NOT NULL, [SourceContributionsJson] nvarchar(max) NOT NULL, CONSTRAINT [PK_ConfigurationUnifiedVersionDocuments] PRIMARY KEY ([Version], [DefinitionKey])",
            var name when IsMySql(name) =>
                "`Version` bigint NOT NULL, `DefinitionKey` varchar(191) NOT NULL, `DefinitionIdentity` char(64) NOT NULL, `DisplayName` longtext NOT NULL, `Category` longtext NULL, `FromProject` longtext NOT NULL, `SchemaVersion` int NOT NULL, `SchemaHash` longtext NOT NULL, `EffectiveValueVersion` bigint NULL, `Json` longtext NOT NULL, `SourceContributionsJson` longtext NOT NULL, PRIMARY KEY (`Version`, `DefinitionKey`)",
            _ =>
                "\"Version\" bigint NOT NULL, \"DefinitionKey\" text NOT NULL, \"DefinitionIdentity\" char(64) NOT NULL, \"DisplayName\" text NOT NULL, \"Category\" text NULL, \"FromProject\" text NOT NULL, \"SchemaVersion\" integer NOT NULL, \"SchemaHash\" text NOT NULL, \"EffectiveValueVersion\" bigint NULL, \"Json\" text NOT NULL, \"SourceContributionsJson\" text NOT NULL, PRIMARY KEY (\"Version\", \"DefinitionKey\")"
        };
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
