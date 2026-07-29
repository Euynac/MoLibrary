namespace Monica.Framework.UI.UIExecutionPipeline.Models;

/// <summary>
/// Filters observed execution plans by business-operation classification.
/// </summary>
public enum ExecutionPipelineBusinessFilter
{
    /// <summary>Includes business and non-business operations.</summary>
    All,

    /// <summary>Includes only business operations.</summary>
    BusinessOnly,

    /// <summary>Includes only non-business operations.</summary>
    NonBusinessOnly
}
