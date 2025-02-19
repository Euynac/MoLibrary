using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocksPlatform.SeedWork;

public static class GlobalServiceProvider
{
    private static IServiceProvider _provider = null!;

    /// <summary>
    /// Globally shared service provider instance.
    /// </summary>
    public static IServiceProvider Provider
    {
        get
        {
            if (_provider == null)
            {
                throw new InvalidOperationException("Global service provider has not been configured.");
            }

            return _provider;
        }
        set => _provider = value;
    }

    public static T GetService<T>() where T:notnull
    {
        return Provider.GetRequiredService<T>();
    }

}