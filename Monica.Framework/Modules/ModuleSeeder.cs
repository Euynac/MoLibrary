using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Monica.Core;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;
using Monica.Framework.Seeder.Abstractions;
using Monica.Framework.Seeder.Services;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

public static class ModuleSeederBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Configure the Seeder module
        /// </summary>
        public ModuleSeederGuide AddSeeder(Action<ModuleSeederOption>? action = null)
        {
            return builder.AddModule<ModuleSeeder, ModuleSeederOption, ModuleSeederGuide>(action);
        }
    }
}

[ModuleKey(BuiltInModuleKey.Seeder)]
public class ModuleSeeder(ModuleSeederOption option) : WebModuleBase<ModuleSeeder, ModuleSeederOption, ModuleSeederGuide>(option), IBusinessTypeIterator
{
    private readonly List<Type> _seedTypes = [];

    public override void ConfigureApplicationBuilder(IApplicationBuilder app)
    {
        foreach (var type in _seedTypes)
        {
            var seed = (ISeeder) ActivatorUtilities.CreateInstance(app.ApplicationServices, type);
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

            if (type is { IsClass: true, IsAbstract: false} && type.IsSubclassOf(typeof(SeederBase)))
            {
                _seedTypes.Add(type);
            }
            yield return type;
        }
    }
}

public class ModuleSeederGuide : WebModuleGuide<ModuleSeeder, ModuleSeederOption, ModuleSeederGuide>
{

}

public class ModuleSeederOption : ModuleOptions<ModuleSeeder>
{
}

///// <summary>
///// Indicates that this method is a Static constructor for execution after AppInit
///// </summary>
//[AttributeUsage(AttributeTargets.Constructor)]
//public class RunAfterAppInitAttribute : Attribute
//{

//}
