using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Repository.Transaction;

namespace MoLibrary.Framework.Features.MoSeeder;

/// <summary>
/// 指定该类是种子类，启动服务后将会自动执行一遍
/// </summary>
public abstract class MoSeeder(IServiceProvider serviceProvider) : IMoSeeder
{
    public async Task SeedAsync()
    {
        // TODO 优化不依赖UnitOfWork的种子方法
        var manager = serviceProvider.GetRequiredService<IMoUnitOfWorkManager>();
        using var uow = manager.Begin();
        await SeedingAsync();
        await uow.CompleteAsync();
    }

    public abstract Task SeedingAsync();
}

public interface IMoSeeder
{
    /// <summary>
    /// 执行种子方法
    /// </summary>
    /// <returns></returns>
    public Task SeedAsync();
}