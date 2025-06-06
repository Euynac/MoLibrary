using System.ComponentModel;
using System.Globalization;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.Utils;

namespace MoLibrary.Repository.EntityInterfaces;

public interface IHasExtraProperties
{
    ExtraPropertyDictionary ExtraProperties { get; }
}


[Serializable]
public class ExtraPropertyDictionary : Dictionary<string, object?>
{
    public ExtraPropertyDictionary()
    {

    }

    public ExtraPropertyDictionary(IDictionary<string, object?> dictionary)
        : base(dictionary)
    {
    }
}


public static class ExtraPropertyDictionaryExtensions
{
    public static T ToEnum<T>(this ExtraPropertyDictionary extraPropertyDictionary, string key)
        where T : Enum
    {
        if (extraPropertyDictionary[key]!.GetType() == typeof(T))
        {
            return (T)extraPropertyDictionary[key]!;
        }

        extraPropertyDictionary[key] = Enum.Parse(typeof(T), extraPropertyDictionary[key]!.ToString()!, ignoreCase: true);
        return (T)extraPropertyDictionary[key]!;
    }

    public static object ToEnum(this ExtraPropertyDictionary extraPropertyDictionary, string key, Type enumType)
    {
        if (!enumType.IsEnum || extraPropertyDictionary[key]!.GetType() == enumType)
        {
            return extraPropertyDictionary[key]!;
        }

        extraPropertyDictionary[key] = Enum.Parse(enumType, extraPropertyDictionary[key]!.ToString()!, ignoreCase: true);
        return extraPropertyDictionary[key]!;
    }

    public static bool HasSameItems(
        this ExtraPropertyDictionary dictionary,
        ExtraPropertyDictionary otherDictionary)
    {
        if (dictionary.Count != otherDictionary.Count)
        {
            return false;
        }

        foreach (var key in dictionary.Keys)
        {
            if (!otherDictionary.ContainsKey(key) ||
                dictionary[key]?.ToString() != otherDictionary[key]?.ToString())
            {
                return false;
            }
        }

        return true;
    }
}


public static class HasExtraPropertiesExtensions
{
    public static bool HasProperty(this IHasExtraProperties source, string name)
    {
        return source.ExtraProperties.ContainsKey(name);
    }

    public static object? GetProperty(this IHasExtraProperties source, string name, object? defaultValue = null)
    {
        return source.ExtraProperties.GetOrDefault(name)
               ?? defaultValue;
    }

    public static TProperty? GetProperty<TProperty>(this IHasExtraProperties source, string name, TProperty? defaultValue = default)
    {
        var value = source.GetProperty(name);
        if (value == null)
        {
            return defaultValue;
        }

        if (TypeHelper.IsPrimitiveExtended(typeof(TProperty), includeEnums: true))
        {
            var conversionType = typeof(TProperty);
            if (conversionType.IsNullableValueType())
            {
                conversionType = conversionType.GetFirstGenericArgumentIfNullable();
            }

            if (conversionType == typeof(Guid))
            {
                return (TProperty)TypeDescriptor.GetConverter(conversionType).ConvertFromInvariantString(value.ToString()!)!;
            }

            if (conversionType.IsEnum)
            {
                return (TProperty)value;
            }

            return (TProperty)Convert.ChangeType(value, conversionType, CultureInfo.InvariantCulture);
        }

        throw new Exception("GetProperty<TProperty> does not support non-primitive types. Use non-generic GetProperty method and handle type casting manually.");
    }

    public static TSource SetProperty<TSource>(
        this TSource source,
        string name,
        object? value,
        bool validate = true)
        where TSource : IHasExtraProperties
    {
        if (validate)
        {
            CheckValue(source, name, value);
        }

        source.ExtraProperties[name] = value;

        return source;
    }
    public static void CheckValue(
        IHasExtraProperties extensibleObject,
        string propertyName,
        object? value)
    {
        //var validationErrors = GetValidationErrors(
        //    extensibleObject,
        //    propertyName,
        //    value
        //);

        //if (validationErrors.Any())
        //{
        //    throw new AbpValidationException(validationErrors);
        //}
    }

    public static TSource RemoveProperty<TSource>(this TSource source, string name)
        where TSource : IHasExtraProperties
    {
        source.ExtraProperties.Remove(name);
        return source;
    }


    public static void SetExtraPropertiesToRegularProperties(this IHasExtraProperties source)
    {
        var properties = source.GetType().GetProperties()
            .Where(x => source.ExtraProperties.ContainsKey(x.Name)
                        && x.GetSetMethod(true) != null)
            .ToList();

        foreach (var property in properties)
        {
            property.SetValue(source, source.ExtraProperties[property.Name]);
            source.RemoveProperty(property.Name);
        }
    }

    public static bool HasSameExtraProperties(
         this IHasExtraProperties source,
         IHasExtraProperties other)
    {
        Check.NotNull(source, nameof(source));
        Check.NotNull(other, nameof(other));

        return source.ExtraProperties.HasSameItems(other.ExtraProperties);
    }
}
