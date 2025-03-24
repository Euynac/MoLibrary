using System.Reflection;
using JetBrains.Annotations;
using MoLibrary.Tool.Utils;


namespace BuildingBlocksPlatform.BlobContainer.Abstract;

public class BlobContainerNameAttribute : Attribute
{
    public BlobContainerNameAttribute(string name)
    {
        Check.NotNullOrWhiteSpace(name, nameof(name));

        Name = name;
    }

    public string Name { get; }

    public virtual string GetName(Type type)
    {
        return Name;
    }

    public static string GetContainerName<T>()
    {
        return GetContainerName(typeof(T));
    }

    public static string GetContainerName(Type type)
    {
        var nameAttribute = type.GetCustomAttribute<BlobContainerNameAttribute>();

        if (nameAttribute == null) return type.FullName!;

        return nameAttribute.GetName(type);
    }
}