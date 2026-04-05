namespace Monica.Framework.Seeder.Abstractions;

public interface ISeeder
{
    /// <summary>
    /// Execute seed method
    /// </summary>
    /// <returns></returns>
    public Task SeedAsync();
}