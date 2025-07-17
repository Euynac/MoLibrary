using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoLibrary.Core.Extensions;

namespace MoLibrary.Core.Module.TypeFinder
{
    public static class MoModuleSystemTypeFinderExtensions
    {
        internal static IDomainTypeFinder? GlobalTypeFinder;
        public static IDomainTypeFinder GetOrCreateMoModuleSystemTypeFinder(this IServiceCollection services, Action<ModuleCoreOptionTypeFinder>? configure = null)
        {
            if (GlobalTypeFinder is not null) return GlobalTypeFinder;
            services.ConfigActionWrapper(configure, out var config);
            var typeFinder = Activator.CreateInstance(typeof(MoDomainTypeFinder), config)!;
            services.TryAddSingleton(typeFinder);
            GlobalTypeFinder = (IDomainTypeFinder) typeFinder;
            return GlobalTypeFinder;
        }
    }
}
