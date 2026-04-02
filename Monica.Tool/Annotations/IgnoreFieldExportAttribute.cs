namespace Monica.Tool.Annotations;

/// <summary>
/// Excludes the annotated property from <c>FieldsToKeyValues</c> export output.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class IgnoreFieldExportAttribute : Attribute;
