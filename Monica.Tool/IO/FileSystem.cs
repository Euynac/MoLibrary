using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Monica.Tool.Extensions;
using Monica.Tool.Randomization;

namespace Monica.Tool.IO;

/// <summary>
/// Provides lightweight file-system helpers used across Monica modules.
/// </summary>
public static class FileSystem
{
    /// <summary>
    /// Rename a file.
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="newName"></param>
    public static FileInfo? Rename(this FileInfo fileInfo, string newName)
    {
        if (!fileInfo.Exists) return null;
        try
        {
            var newPath = Path.Combine(fileInfo.Directory!.FullName, newName);
            fileInfo.MoveTo(newPath);
            return new FileInfo(newPath);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Determines whether the supplied path points to a directory.
    /// </summary>
    /// <param name="path"></param>
    /// <returns>If file not exist or is a file, return false.</returns>
    public static bool IsDirectory(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
    }

    /// <summary>
    /// The same as Environment.GetFolderPath();
    /// </summary>
    /// <param name="folder">Default is desktop directory</param>
    /// <returns></returns>
    public static string GetFolderPath(Environment.SpecialFolder folder = Environment.SpecialFolder.DesktopDirectory)
    {
        return Environment.GetFolderPath(folder);
    }
    /// <summary>
    /// Read the assembly file in a way that does not occupy the file
    /// </summary>
    /// <param name="fileUrl"></param>
    /// <returns></returns>
    public static Assembly LoadAssembly(string fileUrl)
    {
        var dllFileData = File.ReadAllBytes(fileUrl);//这样加载之后不会占用dll
        return Assembly.Load(dllFileData);
    }
    /// <summary>
    /// Get all the content of the embedded resource file of the assembly that calls this method (note that you need to change the file you get to Embedded Resource)
    /// </summary>
    /// <param name="fileUri">The path of the file to read. Format: folder. File name. Extension name</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string? ReadEmbeddedResource(string fileUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileUri);

        var assembly = Assembly.GetCallingAssembly();
        var assemblyName = assembly.GetName().Name;
        using var stream = assembly.GetManifestResourceStream(fileUri) ??
                           (assemblyName == null
                               ? null
                               : assembly.GetManifestResourceStream($"{assemblyName}.{fileUri.TrimStart('.')}"));
        if (stream == null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Returns the file name and extension of the specified path string.
    /// </summary>
    /// <param name="path">The path string from which to obtain the file name and extension.</param>
    /// <param name="needExtension">if to get file name with file extension.</param>
    /// <returns>The characters after the last directory character in <paramref name="path">path</paramref>. If the last character of <paramref name="path">path</paramref> is a directory or volume separator character, this method returns <see cref="F:System.String.Empty"></see>. If <paramref name="path">path</paramref> is null, this method returns null.</returns>
    public static string GetFileName(string path, bool needExtension = true)
    {
        return needExtension ? Path.GetFileName(path) : Path.GetFileNameWithoutExtension(path);
    }
    /// <summary>
    /// The same as Path.GetRandomFileName();
    /// </summary>
    /// <returns></returns>
    public static string GetRandomFileName()
    {
        return Path.GetRandomFileName();
    }
    /// <summary>
    /// Get timestamp of now based random file name.
    /// </summary>
    /// <param name="randomDeep">The random number count that will generate after timestamp</param>
    /// <returns></returns>
    public static string GetTimestampRandomFileName(int randomDeep = 3)
    {
        return DateTime.Now.ToTimeStamp() + RandomValueGenerator.NextString(randomDeep);
    }

    /// <summary>
    /// Validates whether the supplied string matches a Windows-style file path pattern.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    /// Note that "" is escaped into "
    /// Using a named capture group, this pattern string can match path, filename, name, ext, but cannot match folder and file names starting with ., and the existence of \\
    public static bool IsPath(string path)
    {
        return path.IsMatch(@"^(?<path>(?:[a-zA-Z]:)?\\?(?:(?!\.)[^\\\?\/\*\|<>:""]+\\?)*?)(?:(?<filename>(?<name>[^\\\?\/\*\|<>:""]+?)\.(?<ext>[^.\\\?\/\*\|<>:""]+)))?$");
    }
    /// <summary>
    /// Get the current directory name based on the file path
    /// </summary>
    /// <param name="path"></param>
    /// <returns>Unable to obtain return null</returns>
    public static string? GetDirectoryName(string path)//当前目录名
    {
        return Directory.GetParent(path)?.Name;
    }
    /// <summary>
    /// Get the current directory path based on the file path
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string? GetDirectoryPath(string path)//目录路径
    {
        return Path.GetDirectoryName(path);
    }

    /// <summary>
    /// The same as Path.Combine
    /// </summary>
    /// <returns></returns>
    public static string CombineDirectoryWithFileName(string directory, string fileName)
    {
        return Path.Combine(directory, fileName);
    }

    /// <summary>
    /// Overwrite file
    /// </summary>
    /// <param name="path"></param>
    /// <param name="content"></param>
    /// <param name="ensureDirectory">If the directory does not exist, create it</param>
    public static void WriteFile(string path, string content, bool ensureDirectory = true)
    {
        var stringBuilder = new StringBuilder(content);
        WriteFile(path, stringBuilder, ensureDirectory);
    }

    /// <summary>
    /// Append to the end of the file
    /// </summary>
    /// <param name="path"></param>
    /// <param name="content"></param>
    /// <param name="ensureDirectory">If the directory does not exist, create it</param>
    public static void AppendFile(string path, string content, bool ensureDirectory = true)
    {
        var stringBuilder = new StringBuilder(content);
        AppendFile(path, stringBuilder, ensureDirectory);
    }

    /// <summary>
    /// Append to the end of the file
    /// </summary>
    /// <param name="path"></param>
    /// <param name="content"></param>
    /// <param name="ensureDirectory">If the directory does not exist, create it</param>
    public static void AppendFile(string path, StringBuilder content, bool ensureDirectory = true)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureParentDirectory(path, ensureDirectory);

        using var writer = new StreamWriter(path, append: true);
        writer.Write(content);
    }
    /// <summary>
    /// Overwrite file
    /// </summary>
    /// <param name="path"></param>
    /// <param name="content"></param>
    /// <param name="ensureDirectory">If the directory does not exist, create it</param>
    public static void WriteFile(string path, StringBuilder content, bool ensureDirectory = true)
    {
        ArgumentNullException.ThrowIfNull(content);
        EnsureParentDirectory(path, ensureDirectory);

        using var writer = new StreamWriter(path, append: false);
        writer.Write(content);
    }
    /// <summary>
    /// Delete file at given path.
    /// </summary>
    /// <param name="path"></param>
    /// <returns>If not exist or something wrong, return false.</returns>
    public static bool Delete(string path)
    {
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }


    /// <summary>
    /// The same as Directory.GetCurrentDirectory().
    /// <para>Get the current working directory of the application.</para>
    /// </summary>
    /// <returns></returns>

    public static string GetCurrentDirectory() => Directory.GetCurrentDirectory();
        
    /// <summary>
    /// Read file information
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    [Obsolete("use File.ReadAllText instead.")]
    public static StringBuilder ReadFile(string path) //读取一般大小文件（未测试多大）
    {
        try
        {
            using var fileStream = new FileStream(path, FileMode.Open);
            var reader = new StreamReader(fileStream);
            var result = new StringBuilder(reader.ReadToEnd());
            reader.Close();
            return result;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new StringBuilder();
        }
    }
    /// <summary>
    ///  Directory will have ending '\'.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static (string? directory, string? fileName) SeparateDirectoryAndFileName(string path)
    {
        var directory = Path.GetDirectoryName(path);
        // if directory don't have ending \', then add it.
        if (!string.IsNullOrEmpty(directory) && !Path.EndsInDirectorySeparator(directory))
        {
            directory += Path.DirectorySeparatorChar;
        }
        var fileName = Path.GetFileName(path);
        return (directory, fileName);
    }

    private static void EnsureParentDirectory(string path, bool ensureDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!ensureDirectory)
        {
            return;
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
