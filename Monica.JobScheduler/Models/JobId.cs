namespace Monica.JobScheduler.Models;

/// <summary>
/// Identifies one owner-scoped job definition inside a scheduler scope.
/// </summary>
/// <param name="OwnerKey">The stable owner identity of the definition.</param>
/// <param name="JobKey">The owner-scoped logical job key.</param>
public readonly record struct JobId(string OwnerKey, string JobKey)
{
    /// <summary>
    /// Validates both identity components against the persistence-safe limits.
    /// </summary>
    public void Validate()
    {
        JobSchedulerIdentity.ValidateStandard(OwnerKey, nameof(OwnerKey));
        JobSchedulerIdentity.ValidateJobKey(JobKey, nameof(JobKey));
    }

    public override string ToString() => $"{OwnerKey}/{JobKey}";
}
