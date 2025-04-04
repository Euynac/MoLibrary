using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Framework.Features.MoSeeder;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 运行种子方法
    /// </summary>
    /// <param name="app"></param>
    /// <param name="related"></param>
    /// <returns></returns>
    public static void UseMoSeeder(this IApplicationBuilder app, IEnumerable<Assembly> related)
    {
        foreach (var assembly in related)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.TypeInitializer is { } initializer && !type.Attributes.HasFlag(TypeAttributes.BeforeFieldInit) && initializer.GetCustomAttribute<RunAfterAppInitAttribute>() != null)
                {
                    Task.Run(() =>
                    {
                        RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                    });
                }

                if (type.IsSubclassOf(typeof(MoSeeder)))
                {
                    var seed = (IMoSeeder) ActivatorUtilities.CreateInstance(app.ApplicationServices, type);
                    seed.SeedAsync();
                    //TODO 优化种子方法执行策略
                }
            }
        }
      
    }
}