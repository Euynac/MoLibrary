using System.Collections;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Monica.Configuration.Binding;

/// <summary>
/// Binds Monica configuration objects while treating configured collection sections as replacements for CLR defaults.
/// </summary>
internal static class MonicaConfigurationBinder
{
    /// <summary>
    /// Creates and binds a new options instance from the supplied configuration section.
    /// </summary>
    /// <typeparam name="TOptions">Options type to create.</typeparam>
    /// <param name="configuration">Configuration section to bind.</param>
    /// <returns>The bound options instance.</returns>
    public static TOptions Get<TOptions>(IConfiguration configuration)
        where TOptions : class, new()
    {
        var options = new TOptions();
        Bind(configuration, options);
        return options;
    }

    /// <summary>
    /// Registers an options binding that replaces configured collection defaults instead of appending to them.
    /// </summary>
    /// <typeparam name="TOptions">Options type being registered.</typeparam>
    /// <param name="optionsBuilder">Options builder to configure.</param>
    /// <param name="configuration">Configuration section to bind.</param>
    /// <returns>The same options builder for chaining.</returns>
    public static OptionsBuilder<TOptions> BindOptions<TOptions>(
        OptionsBuilder<TOptions> optionsBuilder,
        IConfiguration configuration)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(configuration);

        var services = optionsBuilder.Services;
        var name = optionsBuilder.Name;
        services.AddSingleton<IOptionsChangeTokenSource<TOptions>>(
            new ConfigurationChangeTokenSource<TOptions>(name, configuration));
        services.AddSingleton<IConfigureOptions<TOptions>>(
            new CollectionReplacingNamedConfigureFromConfigurationOptions<TOptions>(name, configuration));
        return optionsBuilder;
    }

    /// <summary>
    /// Binds configuration into an existing instance after clearing configured collection defaults.
    /// </summary>
    /// <param name="configuration">Configuration section to bind.</param>
    /// <param name="options">Existing options instance.</param>
    public static void Bind(IConfiguration configuration, object options)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);

        PrepareConfiguredCollections(options, configuration, new HashSet<object>(ReferenceEqualityComparer.Instance));
        configuration.Bind(options);
    }

    private static void PrepareConfiguredCollections(
        object instance,
        IConfiguration configuration,
        ISet<object> visited)
    {
        if (!visited.Add(instance))
        {
            return;
        }

        foreach (var property in GetBindableProperties(instance.GetType()))
        {
            var propertySection = configuration.GetSection(GetConfigurationPropertyName(property));
            if (IsReplaceableCollection(property.PropertyType))
            {
                if (propertySection.GetChildren().Any())
                {
                    ReplaceCollectionDefault(instance, property);
                }

                continue;
            }

            if (!IsComplexObject(property.PropertyType) || !propertySection.Exists())
            {
                continue;
            }

            var value = property.GetValue(instance);
            if (value is not null)
            {
                PrepareConfiguredCollections(value, propertySection, visited);
            }
        }
    }

    private static IEnumerable<PropertyInfo> GetBindableProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetMethod is not null && property.GetIndexParameters().Length == 0);
    }

    private static void ReplaceCollectionDefault(object instance, PropertyInfo property)
    {
        var currentValue = property.GetValue(instance);
        if (TryClearCollection(currentValue))
        {
            return;
        }

        if (property.SetMethod is { IsPublic: true })
        {
            property.SetValue(instance, null);
        }
    }

    private static bool TryClearCollection(object? value)
    {
        switch (value)
        {
            case null:
                return false;
            case Array:
                return false;
            case IDictionary { IsFixedSize: false, IsReadOnly: false } dictionary:
                dictionary.Clear();
                return true;
            case IList { IsFixedSize: false, IsReadOnly: false } list:
                list.Clear();
                return true;
        }

        var clearMethod = value.GetType().GetMethod(
            nameof(ICollection<object>.Clear),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);

        if (clearMethod?.ReturnType != typeof(void))
        {
            return false;
        }

        clearMethod.Invoke(value, null);
        return true;
    }

    private static bool IsReplaceableCollection(Type type)
    {
        if (type == typeof(string))
        {
            return false;
        }

        return type.IsArray
               || typeof(IDictionary).IsAssignableFrom(type)
               || typeof(IList).IsAssignableFrom(type)
               || ImplementsGenericInterface(type, typeof(IDictionary<,>))
               || ImplementsGenericInterface(type, typeof(ICollection<>));
    }

    private static bool IsComplexObject(Type type)
    {
        var actual = Nullable.GetUnderlyingType(type) ?? type;
        return actual.IsClass
               && !IsSimpleValue(actual)
               && !IsReplaceableCollection(actual)
               && !typeof(Delegate).IsAssignableFrom(actual);
    }

    private static bool IsSimpleValue(Type type)
    {
        return type.IsPrimitive
               || type.IsEnum
               || type == typeof(string)
               || type == typeof(decimal)
               || type == typeof(DateTime)
               || type == typeof(DateTimeOffset)
               || type == typeof(TimeSpan)
               || type == typeof(Uri)
               || type == typeof(Guid);
    }

    private static bool ImplementsGenericInterface(Type type, Type genericInterfaceDefinition)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == genericInterfaceDefinition)
        {
            return true;
        }

        return type.GetInterfaces()
            .Any(candidate => candidate.IsGenericType
                              && candidate.GetGenericTypeDefinition() == genericInterfaceDefinition);
    }

    private static string GetConfigurationPropertyName(PropertyInfo property)
    {
        return property.GetCustomAttribute<ConfigurationKeyNameAttribute>()?.Name ?? property.Name;
    }
}

internal sealed class CollectionReplacingNamedConfigureFromConfigurationOptions<TOptions>(
    string? name,
    IConfiguration configuration)
    : ConfigureNamedOptions<TOptions>(name, options => MonicaConfigurationBinder.Bind(configuration, options))
    where TOptions : class
{
}
