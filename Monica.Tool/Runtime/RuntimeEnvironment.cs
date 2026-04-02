namespace Monica.Tool.Utils;

/// <summary>
/// Exposes process-level environment flags used by Monica infrastructure components.
/// </summary>
public static class RuntimeEnvironment
{
    /// <summary>
    /// Returns <see langword="true"/> when <c>ASPNETCORE_ENVIRONMENT</c> is set to <c>Development</c>.
    /// </summary>
    public static bool IsDevelopment() => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.Equals("Development", StringComparison.OrdinalIgnoreCase) is true;

    /// <summary>
    /// Indicates whether the current process is executing a database migration workflow.
    /// </summary>
    public static bool IsRunningMigration { get; set; } = false;
}
