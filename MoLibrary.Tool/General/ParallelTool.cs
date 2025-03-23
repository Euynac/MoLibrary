using System;
using System.Linq;

namespace MoLibrary.Tool.General;

public class ParallelTool
{
    /// <summary>
    /// Parallel doing something and collect the result into a ParallelQuery.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="func"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public static ParallelQuery<T> ParallelSelect<T>(Func<T> func, int count) =>
        Enumerable.Range(0, count)
            .AsParallel()
            .WithDegreeOfParallelism(Environment.ProcessorCount - 1)
            .Select(_ => func.Invoke());
}