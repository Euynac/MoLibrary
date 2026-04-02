namespace Monica.WebApi.Annotations;


/// <summary>
/// Marks the method that should be exposed on the generated service contract.
/// Use this when an application service hides a base member with <c>new</c> or a different
/// signature but still wants that member to win during contract generation.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class OverrideServiceAttribute(int order = 0) : Attribute
{
    /// <summary>
    /// When multiple members with the same signature exist, the member with the highest order is
    /// selected for the generated interface.
    /// </summary>
    public int Order { get; set; } = order;
}
