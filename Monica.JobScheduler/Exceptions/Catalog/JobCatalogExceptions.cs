namespace Monica.JobScheduler.Exceptions.Catalog;

public class JobCatalogException(string message) : InvalidOperationException(message);

public class JobCatalogConflictException(string message) : JobCatalogException(message);

public sealed class JobCatalogNotFoundException(string message) : JobCatalogException(message);

public sealed class JobPolicyConcurrencyException(string jobKey)
    : JobCatalogConflictException($"The policy for job '{jobKey}' changed before this update was applied.");
