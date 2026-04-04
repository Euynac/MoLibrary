using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Interfaces;
using Monica.Core.Modularity.Models;
using Monica.Framework.MoSeeder;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleSeederBuilderExtensions
{
    extension(Mo)
    {
        /// <summary>
        /// Configure the Seeder module
        /// </summary>
        public static ModuleSeederGuide AddSeeder(Action<ModuleSeederOption>? action = null)
        {
            return new ModuleSeederGuide().Register(action);
        }
    }
}

[ModuleKey(EMoModuleKey.Seeder)]
public class ModuleSeeder(ModuleSeederOption option) : MoModule<ModuleSeeder, ModuleSeederOption, ModuleSeederGuide>(option), IWantIterateBusinessTypes
{
    private readonly List<Type> _seedTypes = [];

    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        foreach (var type in _seedTypes)
        {
            var seed = (IMoSeeder) ActivatorUtilities.CreateInstance(app.ApplicationServices, type);
            seed.SeedAsync();
            //TODO optimize seed method execution strategy
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
///// Indicates that this method is a Static constructor for execution after AppInit
///// </summary>
//[AttributeUsage(AttributeTargets.Constructor)]
//public class RunAfterAppInitAttribute : Attribute
//{

//}