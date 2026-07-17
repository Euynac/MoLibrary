namespace Monica.Configuration.EfCore.DbContext;

internal static class ConfigurationDbSchema
{
    internal const string DefinitionIdentityIndex = "UX_ConfigDef_Identity";
    internal const string DefinitionPublishIdentityIndex = "IX_ConfigDefPub_Identity_Time";
    internal const string EffectiveValueIdentityIndex = "UX_ConfigValue_Identity";
    internal const string ValueHistoryIdentityIndex = "IX_ConfigHist_Identity_Depth_Time";
    internal const string UnifiedVersionDocumentIdentityIndex = "UX_ConfigVerDoc_Identity_Version";
}
