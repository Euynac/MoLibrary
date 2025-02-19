using Microsoft.Extensions.DependencyInjection;


namespace BuildingBlocksPlatform.SeedWork;

/// <summary>
/// 指定该类是种子类，启动服务后将会自动执行一遍
/// </summary>
public abstract class OurSeeder(IServiceProvider serviceProvider) : IOurSeeder
{
    public async Task SeedAsync()
    {
        var manager = serviceProvider.GetRequiredService<IMoUnitOfWorkManager>();
        using var uow = manager.Begin();
        await SeedingAsync();
        await uow.CompleteAsync();
    }

    public abstract Task SeedingAsync();
}

public interface IOurSeeder
{
    /// <summary>
    /// 执行种子方法
    /// </summary>
    /// <returns></returns>
    public Task SeedAsync();
}