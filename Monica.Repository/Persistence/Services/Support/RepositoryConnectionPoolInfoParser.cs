using System.Data.Common;
using System.Globalization;
using Monica.Repository.Persistence.Models;

namespace Monica.Repository.Persistence.Services.Support;

/// <summary>
/// Parses ADO.NET connection pool keywords from a provider connection string into
/// <see cref="RepositoryConnectionPoolInfo"/>. Keyword lookup is case-insensitive and
/// covers the synonyms used by mainstream providers (Npgsql, Microsoft.Data.SqlClient,
/// MySqlConnector). Values that are missing or invalid are reported as <c>null</c>
/// so the model can fall back to shared provider defaults.
/// </summary>
public static class RepositoryConnectionPoolInfoParser
{
    private static readonly string[] MAX_POOL_SIZE_KEYS = ["Max Pool Size", "Maximum Pool Size"];
    private static readonly string[] MIN_POOL_SIZE_KEYS = ["Min Pool Size", "Minimum Pool Size"];
    private const string POOLING_KEY = "Pooling";

    /// <summary>
    /// Parses pool settings from a provider connection string.
    /// Returns <c>null</c> when the string is empty or cannot be parsed at all.
    /// </summary>
    /// <param name="connectionString">Connection string of the inspected connection.</param>
    public static RepositoryConnectionPoolInfo? Parse(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        try
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            return new RepositoryConnectionPoolInfo
            {
                Pooling = TryGetBoolean(builder, POOLING_KEY),
                MaxPoolSize = TryGetNonNegativeInt32(builder, MAX_POOL_SIZE_KEYS),
                MinPoolSize = TryGetNonNegativeInt32(builder, MIN_POOL_SIZE_KEYS)
            };
        }
        catch
        {
            return null;
        }
    }

    private static bool? TryGetBoolean(DbConnectionStringBuilder builder, string key)
    {
        if (builder.TryGetValue(key, out var value) && bool.TryParse(FormatValue(value), out var flag))
        {
            return flag;
        }

        return null;
    }

    private static int? TryGetNonNegativeInt32(DbConnectionStringBuilder builder, string[] keys)
    {
        foreach (var key in keys)
        {
            if (builder.TryGetValue(key, out var value)
                && int.TryParse(FormatValue(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                && number >= 0)
            {
                return number;
            }
        }

        return null;
    }

    private static string? FormatValue(object? value)
    {
        return value?.ToString()?.Trim();
    }
}
