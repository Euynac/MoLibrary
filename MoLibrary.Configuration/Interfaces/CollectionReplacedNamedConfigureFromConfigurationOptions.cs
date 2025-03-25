using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Configuration.Interfaces;

public class MoExtendedOptionsBuilderConfigurationExtensions
{
    /// <summary>
    /// Registers a configuration instance which <typeparamref name="TOptions" /> will bind against.
    /// </summary>
    /// <typeparam name="TOptions">The options type to be configured.</typeparam>
    /// <param name="optionsBuilder">The options builder to add the services to.</param>
    /// <param name="config">The configuration being bound.</param>
    /// <param name="configureBinder">Used to configure the <see cref="T:Microsoft.Extensions.Configuration.BinderOptions" />.</param>
    /// <returns>The <see cref="T:Microsoft.Extensions.Options.OptionsBuilder`1" /> so that additional calls can be chained.</returns>
    [RequiresDynamicCode("Binding strongly typed objects to configuration values may require generating dynamic code at runtime.")]
    [RequiresUnreferencedCode("TOptions's dependent types may have their members trimmed. Ensure all required members are preserved.")]
    public static OptionsBuilder<TOptions> Bind<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TOptions>(OptionsBuilder<TOptions> optionsBuilder,
        IConfiguration config,
        Action<BinderOptions>? configureBinder)
        where TOptions : class
    {
        var services = optionsBuilder.Services;
        var name = optionsBuilder.Name;
        services.AddOptions();
        services.AddSingleton<IOptionsChangeTokenSource<TOptions>>(new ConfigurationChangeTokenSource<TOptions>(name, config));
        services.AddSingleton<IConfigureOptions<TOptions>>(new CollectionReplacedNamedConfigureFromConfigurationOptions<TOptions>(name, config, configureBinder));
        return optionsBuilder;
    }
}

/// <summary>
/// 解决默认值、及多来源添加问题  TODO 暂不实现多来源添加问题解决
/// </summary>
/// <typeparam name="TOptions"></typeparam>
public class CollectionReplacedNamedConfigureFromConfigurationOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TOptions> :
    ConfigureNamedOptions<TOptions>
    where TOptions : class
{
    /// <summary>
    /// Constructor that takes the <see cref="T:Microsoft.Extensions.Configuration.IConfiguration" /> instance to bind against.
    /// </summary>
    /// <param name="name">The name of the options instance.</param>
    /// <param name="config">The <see cref="T:Microsoft.Extensions.Configuration.IConfiguration" /> instance.</param>
    [RequiresDynamicCode("Binding strongly typed objects to configuration values may require generating dynamic code at runtime.")]
    [RequiresUnreferencedCode("TOptions's dependent types may have their members trimmed. Ensure all required members are preserved.")]
    public CollectionReplacedNamedConfigureFromConfigurationOptions(string? name, IConfiguration config)
      : this(name, config, _ => { })
    {
    }

    /// <summary>
    /// Constructor that takes the <see cref="T:Microsoft.Extensions.Configuration.IConfiguration" /> instance to bind against.
    /// </summary>
    /// <param name="name">The name of the options instance.</param>
    /// <param name="config">The <see cref="T:Microsoft.Extensions.Configuration.IConfiguration" /> instance.</param>
    /// <param name="configureBinder">Used to configure the <see cref="T:Microsoft.Extensions.Configuration.BinderOptions" />.</param>
    [RequiresDynamicCode("Binding strongly typed objects to configuration values may require generating dynamic code at runtime.")]
    [RequiresUnreferencedCode("TOptions's dependent types may have their members trimmed. Ensure all required members are preserved.")]
    public CollectionReplacedNamedConfigureFromConfigurationOptions(
      string? name,
      IConfiguration config,
      Action<BinderOptions>? configureBinder)
      : base(name, options => config.Bind(DoNotUseDefaultIfConfigurationContains(ref options, config), configureBinder))
    {
        ArgumentNullException.ThrowIfNull(config);
    }

    /// <summary>
    /// 解决Configuration的对于ICollection的行为存在多来源时默认是Append的情况，将其改写为覆盖
    /// </summary>
    /// <param name="options"></param>
    /// <param name="configurations"></param>
    /// <returns></returns>
    public static TOptions DoNotUseDefaultIfConfigurationContains(ref TOptions options, IConfiguration configurations)
    {
        foreach (var collectionProperty in GetAllCollectionProperties(options.GetType()))
        {
            var value = (dynamic?)collectionProperty.GetValue(options);
            if (value == null || value!.Count == 0) continue;
            var section = configurations.GetSection(GetPropertyName(collectionProperty));
            if (section.Exists())
            {
                value!.Clear();
            }
        }

        return options;
    }

    /// <summary>
    /// 获取所有的集合属性
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private static IEnumerable<PropertyInfo> GetAllCollectionProperties([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
    {
        for (var t = type; t != typeof(object); t = t.BaseType)
        {
            if (t == null) continue;
            foreach (var property in t.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (property.PropertyType.IsCollectionType())
                {
                    yield return property;
                }
            }
        }
    }

    /// <summary>
    /// 获取属性在Configuration中的名称
    /// </summary>
    /// <param name="property"></param>
    /// <returns></returns>
    private static string GetPropertyName(PropertyInfo property)
    {
        foreach (var customAttributeData in property.GetCustomAttributesData())
        {
            if (!(customAttributeData.AttributeType != typeof(ConfigurationKeyNameAttribute)))
            {
                if (customAttributeData.ConstructorArguments.Count == 1)
                {
                    var str = customAttributeData.ConstructorArguments[0].Value?.ToString();
                    return !string.IsNullOrWhiteSpace(str) ? str : property.Name;
                }
                break;
            }
        }
        return property.Name;
    }
}