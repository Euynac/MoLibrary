using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoLibrary.Core.Extensions;

namespace MoLibrary.Core.Module.TypeFinder
{
    public static class DomainTypeFinderExtensions
    {
        internal static IDomainTypeFinder? AppDomainTypeFinder;
        internal static IDomainTypeFinder GetOrCreateDomainTypeFinder<T>(this IServiceCollection services, Action<ModuleCoreOptionTypeFinder>? configure = null) where T : IDomainTypeFinder
        {
            if (AppDomainTypeFinder is not null) return AppDomainTypeFinder;
            services.ConfigActionWrapper(configure, out var config);
            var typeFinder = Activator.CreateInstance(typeof(T), config)!;
            services.TryAddSingleton(typeFinder);
            AppDomainTypeFinder = (IDomainTypeFinder) typeFinder;
            return AppDomainTypeFinder;
        }
    }
}
