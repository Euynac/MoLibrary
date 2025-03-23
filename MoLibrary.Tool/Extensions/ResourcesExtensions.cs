using System.IO;

namespace MoLibrary.Tool.Extensions;

public static class ResourcesExtensions
{
    public static MemoryStream ToMemoryStream(this byte[] bytes) => new(bytes);
}