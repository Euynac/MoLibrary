using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.Models;
using MoLibrary.Framework.Features.MoSeeder;

namespace MoLibrary.Framework.Modules;


public static class ModuleSeederBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// 配置 Seeder 模块
        /// </summary>
        public static ModuleSeederGuide AddSeeder(Action<ModuleSeederOption>? action = null)
        {
            return new ModuleSeederGuide().Register(action);
        }
    }
}

public class ModuleSeeder(ModuleSeederOption option) : MoModule<ModuleSeeder, ModuleSeederOption, ModuleSeederGuide>(option), IWantIterateBusinessTypes
{
    private readonly List<Type> _seedTypes = [];
    public override ModuleKey GetModuleKey()
    {
        return EMoModuleKey.Seeder;
    }

    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        foreach (var type in _seedTypes)
        {
            var seed = (IMoSeeder) ActivatorUtilities.CreateInstance(scope.ServiceProvider, type);
            seed.SeedAsync();
            //TODO 优化种子方法执行策略
        }
    }
    public IEnumerable<Type> IterateBusinessTypes(IEnumerable<Type> types)
    {
        foreach (var type in types)
        {
            //if (type.TypeInitializer is { } initializer && !type.Attributes.HasFlag(TypeAttributes.BeforeFieldInit) && initializer.GetCustomAttribute<RunAfterAppInitAttribute>() != null)
            //{
            //    Task.Run(() =>
            //    {
            //        RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            //    });
            //}

            if (type is { IsClass: true, IsAbstract: false} && type.IsSubclassOf(typeof(MoSeeder)))
            {
                _seedTypes.Add(type);
            }
            yield return type;
        }
    }
}

public class ModuleSeederGuide : MoModuleGuide<ModuleSeeder, ModuleSeederOption, ModuleSeederGuide>
{


}

public class ModuleSeederOption : MoModuleOption<ModuleSeeder>
{
}




///// <summary>
///// 指示该方法是用于在AppInit后执行的Static构造方法
///// </summary>
//[AttributeUsage(AttributeTargets.Constructor)]
//public class RunAfterAppInitAttribute : Attribute
//{

//}