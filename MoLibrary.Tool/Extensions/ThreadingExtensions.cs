using System.Threading.Tasks;

namespace MoLibrary.Tool.Extensions;

public static class ThreadingExtensions
{
    /// <summary>
    /// Sync over async method, use it carefully.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="task"></param>
    /// <returns></returns>
    public static T GetResult<T>(this Task<T> task)
    {
        return task.ConfigureAwait(false).GetAwaiter().GetResult();
    }
}