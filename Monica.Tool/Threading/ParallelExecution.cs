namespace Monica.Tool.Threading;

public static class ParallelExecution
{
    /// <summary>
    /// Parallel doing something and collect the result into a ParallelQuery.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="func"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    public static ParallelQuery<T> ParallelSelect<T>(Func<T> func, int count)
    {
        ArgumentNullException.ThrowIfNull(func);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return Enumerable.Range(0, count)
            .AsParallel()
            .WithDegreeOfParallelism(Math.Max(1, Environment.ProcessorCount - 1))
            .Select(_ => func.Invoke());
    }
}
