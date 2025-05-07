using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoLibrary.Core.Extensions;

namespace MoLibrary.Core.Module.TypeFinder
{
    public static class DomainTypeFinderExtensions
    {
        internal static T AddDomainTypeFinder<T>(this IServiceCollection services, Action<ModuleCoreOptionTypeFinder>? configure = null) where T : MoDomainTypeFinder
        {
            services.ConfigActionWrapper(configure, out var config);
            var typeFinder = Activator.CreateInstance(typeof(T), config)!;
            services.TryAddSingleton(typeFinder);
            return (T)typeFinder;
        }
    }
}
