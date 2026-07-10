using Monica.Configuration.Models;

namespace Monica.Configuration.Abstractions;

/// <summary>
/// Validates, persists, reloads, and reports one configuration mutation group.
/// </summary>
public interface IConfigurationMutationGroupApplyService
{
    /// <summary>
    /// Applies one reviewed mutation group.
    /// </summary>
    /// <param name="request">The group request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The group outcome, including post-commit issues.</returns>
    Task<ConfigurationMutationGroupApplyResult> ApplyAsync(
        ConfigurationMutationGroupApplyRequest request,
        CancellationToken cancellationToken);
}
