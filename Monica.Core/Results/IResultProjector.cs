namespace Monica.Core.Results;

/// <summary>
/// Projects Monica result envelopes into custom external API response models.
/// </summary>
public interface IResultProjector
{
    object Project(IResultEnvelope response);
}
