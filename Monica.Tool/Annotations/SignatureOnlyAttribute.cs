namespace Monica.Tool.Annotations;

/// <summary>
/// Includes the annotated property only when signature generation is configured
/// to opt into explicitly marked members.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SignatureOnlyAttribute : Attribute;
