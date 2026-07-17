namespace Monica.Configuration.EfCore.DbContext;

internal static class ConfigurationDbSchema
{
    internal const string DefinitionIdentityIndex = "UX_ConfigDef_Identity";
    internal const string DefinitionRevisionIndex = "UX_ConfigDefPub_Identity_Revision";
    internal const string DefinitionPublisherIndex = "IX_ConfigDefPublisher_Publisher";
    internal const string EffectiveValueIdentityIndex = "UX_ConfigValue_Identity";
    internal const string ValueHistoryIdentityIndex = "IX_ConfigHist_Identity_Depth_Time";
    internal const string UnifiedVersionDocumentIdentityIndex = "UX_ConfigVerDoc_Identity_Version";
}
