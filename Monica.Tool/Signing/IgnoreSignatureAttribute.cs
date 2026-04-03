namespace Monica.Tool.Signing;

/// <summary>
/// Excludes the annotated property from generated signature payloads.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class IgnoreSignatureAttribute : Attribute;
