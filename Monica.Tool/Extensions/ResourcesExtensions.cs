using System.IO;

namespace Monica.Tool.Extensions;

public static class ResourcesExtensions
{
    public static MemoryStream ToMemoryStream(this byte[] bytes) => new(bytes);
}