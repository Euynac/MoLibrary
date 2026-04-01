namespace Monica.JobScheduler.Models;

/// <summary>
/// Query results, including paginated data and total number
/// </summary>
/// <typeparam name="T">Result item type</typeparam>
/// <param name="Items">Query result item list</param>
/// <param name="TotalCount">Total number of records (before paging)</param>
public record QueryResult<T>(List<T> Items, int TotalCount);
