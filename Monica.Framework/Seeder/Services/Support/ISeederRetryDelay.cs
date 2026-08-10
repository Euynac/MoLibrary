namespace Monica.Framework.Seeder.Services.Support;

internal interface ISeederRetryDelay
{
    Task DelayAsync(
        int failedAttempt,
        Monica.Modules.ModuleSeederOption options,
        CancellationToken cancellationToken);
}
