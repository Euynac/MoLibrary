namespace Monica.Tool.Utils;

public static class UtilsEnvironment
{
    /// <summary>
    /// You can use IHostEnvironment.IsDevelopment() in dependency injection to determine whether it is a development environment.
    /// </summary>
    /// <returns></returns>
    public static bool IsDevelopment() => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.Equals("Development", StringComparison.OrdinalIgnoreCase) is true;

    /// <summary>
    /// Is it in a Migration environment?
    /// </summary>
    public static bool IsRunningMigration { get; set; } = false;

}