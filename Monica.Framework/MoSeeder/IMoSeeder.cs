namespace Monica.Framework.MoSeeder;

public interface IMoSeeder
{
    /// <summary>
    /// Execute seed method
    /// </summary>
    /// <returns></returns>
    public Task SeedAsync();
}