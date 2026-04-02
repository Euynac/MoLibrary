namespace Monica.Tool.Annotations;

/// <summary>
/// Excludes the annotated property from generated signature payloads.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class IgnoreSignatureAttribute : Attribute;
