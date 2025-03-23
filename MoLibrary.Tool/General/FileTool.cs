using System;
using System.IO;
using System.Reflection;
using System.Text;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.Random;

namespace MoLibrary.Tool.General
{
    /// <summary>
    /// Not recommend to use this directly. This collects many command operation of file,
    /// and aims to remind you the official implementation 
    /// </summary>
    public static class FileTool
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
        /// Check if the file path a directory.
        /// </summary>
        /// <param name="path"></param>
        /// <returns>If file not exist or is a file, return false.</returns>
        public static bool IsDirectory(string path)
        {
            if(File.Exists(path)) return false;
            var attr = File.GetAttributes(path);
            return attr.HasFlag(FileAttributes.Directory);
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
        /// 用不会占用文件的方式读取程序集文件
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
        public static string? ReadEmbeddedResource(string fileUri)
        {
            var assembly = Assembly.GetCallingAssembly();
            using var stream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.{fileUri}");
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
            return DateTime.Now.ToTimeStamp() + RandomTool.GetString(3);
        }

        /// <summary>
        /// 检查路径是否是合法路径（Windows）
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        /// 注意""被转义成了"
        /// 有用到具名捕获组，该模式串能匹配出path、filename、name、ext，不能匹配文件夹及文件名为.开头的，以及\\这样的存在
        public static bool IsPath(string path)
        {
            return path.IsMatch(@"^(?<path>(?:[a-zA-Z]:)?\\?(?:(?!\.)[^\\\?\/\*\|<>:""]+\\?)*?)(?:(?<filename>(?<name>[^\\\?\/\*\|<>:""]+?)\.(?<ext>[^.\\\?\/\*\|<>:""]+)))?$");
        }
        /// <summary>
        /// 根据文件路径获取当前目录名
        /// </summary>
        /// <param name="path"></param>
        /// <returns>获取不到返回null</returns>
        public static string? GetDirectoryName(string path)//当前目录名
        {
            return Directory.GetParent(path)?.Name;
        }
        /// <summary>
        /// 根据文件路径获取当前目录路径
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
        /// 覆盖写入文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="content"></param>
        /// <param name="ensureDirectory">如果不存在目录，则创建</param>
        public static void WriteFile(string path, string content, bool ensureDirectory = true)
        {
            var stringBuilder = new StringBuilder(content);
            WriteFile(path, stringBuilder, ensureDirectory);
        }

        /// <summary>
        /// 向文件末尾追加写入
        /// </summary>
        /// <param name="path"></param>
        /// <param name="content"></param>
        /// <param name="ensureDirectory">如果不存在目录，则创建</param>
        public static void AppendFile(string path, string content, bool ensureDirectory = true)
        {
            var stringBuilder = new StringBuilder(content);
            AppendFile(path, stringBuilder, ensureDirectory);
        }

        /// <summary>
        /// 向文件末尾追加写入
        /// </summary>
        /// <param name="path"></param>
        /// <param name="content"></param>
        /// <param name="ensureDirectory">如果不存在目录，则创建</param>
        public static void AppendFile(string path, StringBuilder content, bool ensureDirectory = true)
        {
            if (ensureDirectory)
            {
                Directory.CreateDirectory(Directory.GetParent(path)?.FullName ?? path);
            }
            using var fileStream = new FileStream(path, FileMode.Append);
            var writer = new StreamWriter(fileStream);
            writer.Write(content);
            writer.Flush();
            writer.Close();
        }
        /// <summary>
        /// 覆盖写入文件
        /// </summary>
        /// <param name="path"></param>
        /// <param name="content"></param>
        /// <param name="ensureDirectory">如果不存在目录，则创建</param>
        public static void WriteFile(string path, StringBuilder content, bool ensureDirectory = true)
        {
            if (ensureDirectory)
            {
                Directory.CreateDirectory(Directory.GetParent(path)?.FullName ?? path);
            }
            using var fileStream = new FileStream(path, FileMode.Create);
            var writer = new StreamWriter(fileStream);
            writer.Write(content);
            writer.Flush();
            writer.Close();
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
        /// 读取文件信息
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
            catch (FileNotFoundException)
            {
                return null;
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
            if (!string.IsNullOrEmpty(directory) && !directory.EndsWith(@"\"))
            {
                directory += @"\";
            }
            var fileName = Path.GetFileName(path);
            return (directory, fileName);
        }
    }
}
