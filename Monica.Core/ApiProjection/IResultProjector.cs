using Monica.Tool.Results;

namespace Monica.Core.ApiProjection;

/// <summary>
/// Projects Monica result envelopes into custom external API response models.
/// </summary>
public interface IResultProjector
{
    object? ProjectToObject(IResultEnvelope response);
}

/// <summary>
/// Projects Monica result envelopes into a strongly typed external API response model.
/// </summary>
/// <typeparam name="TCustomResult">The final serialized response model.</typeparam>
public interface IResultProjector<out TCustomResult> : IResultProjector
{
    TCustomResult Project(IResultEnvelope response);

    object? IResultProjector.ProjectToObject(IResultEnvelope response) => Project(response);
}
