using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Repository.Transaction;

namespace MoLibrary.Framework.Features.MoSeeder;

public interface IMoSeeder
{
    /// <summary>
    /// 执行种子方法
    /// </summary>
    /// <returns></returns>
    public Task SeedAsync();
}