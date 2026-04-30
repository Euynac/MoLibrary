using System.Diagnostics.CodeAnalysis;

namespace Monica.Core.Skills;

/// <summary>
/// Generic base class for Monica skills with trim-friendly member discovery.
/// </summary>
/// <typeparam name="TSelf">
/// Concrete skill type whose annotated methods and properties should be preserved for skill discovery.
/// </typeparam>
/// <remarks>
/// Prefer this base for skill authoring. The generic type parameter preserves public and non-public methods
/// and properties for reflection-based script and resource discovery in trimming or Native AOT scenarios.
/// </remarks>
public abstract class Skill<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicMethods
        | DynamicallyAccessedMemberTypes.NonPublicMethods
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.NonPublicProperties)]
    TSelf> : Skill
    where TSelf : Skill<TSelf>
{
    /// <summary>
    /// Initializes a Monica skill authored with the trim-friendly generic base.
    /// </summary>
    protected Skill()
    {
    }
}
