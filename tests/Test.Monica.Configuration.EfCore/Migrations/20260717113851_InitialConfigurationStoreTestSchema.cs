using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Test.Monica.Configuration.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class InitialConfigurationStoreTestSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfigurationDefinitionPublisherStates",
                columns: table => new
                {
                    DefinitionIdentity = table.Column<string>(type: "TEXT", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    PublisherIdentity = table.Column<string>(type: "TEXT", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    PublisherKey = table.Column<string>(type: "TEXT", maxLength: 191, nullable: false, defaultValue: ""),
                    ObservationKind = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: ""),
                    ReloadBehavior = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationDefinitionPublisherStates", x => new { x.DefinitionIdentity, x.PublisherIdentity });
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationDefinitionPublishHistories",
                columns: table => new
                {
                    HistoryId = table.Column<string>(type: "TEXT", nullable: false),
                    DefinitionIdentity = table.Column<string>(type: "TEXT", unicode: false, fixedLength: true, maxLength: 64, nullable: false, defaultValue: ""),
                    DefinitionKey = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    SectionPath = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    Description = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    FromProject = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, defaultValue: ""),
                    Category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, defaultValue: ""),
                    ChangeKind = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    DefinitionRevision = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousSchemaVersion = table.Column<int>(type: "INTEGER", nullable: true),
                    NewSchemaVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    PreviousSchemaHash = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    NewSchemaHash = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    PreviousSchemaJson = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    NewSchemaJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    ChangeSummaryJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    PublisherId = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    PublisherName = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    PublisherVersion = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    PublishedTime = table.Column<DateTime>(type: "timestamp", precision: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationDefinitionPublishHistories", x => x.HistoryId);
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationDefinitions",
                columns: table => new
                {
                    DefinitionKey = table.Column<string>(type: "TEXT", nullable: false),
                    DefinitionIdentity = table.Column<string>(type: "TEXT", unicode: false, fixedLength: true, maxLength: 64, nullable: false, defaultValue: ""),
                    SectionPath = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    Description = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    ClrTypeName = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    FromProject = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false, defaultValue: ""),
                    Category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true, defaultValue: ""),
                    SchemaVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    SchemaHash = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    ReloadBehavior = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    SchemaJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    DefinitionRevision = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationDefinitions", x => x.DefinitionKey);
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationEffectiveValues",
                columns: table => new
                {
                    DefinitionKey = table.Column<string>(type: "TEXT", nullable: false),
                    DefinitionIdentity = table.Column<string>(type: "TEXT", unicode: false, fixedLength: true, maxLength: 64, nullable: false, defaultValue: ""),
                    Json = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    SchemaVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    LastModifiedTime = table.Column<DateTime>(type: "timestamp", precision: 6, nullable: false),
                    LastModifierId = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    LastModifierName = table.Column<string>(type: "TEXT", nullable: true, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationEffectiveValues", x => x.DefinitionKey);
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationMutationGroups",
                columns: table => new
                {
                    GroupId = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    Reason = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    DefinitionKeysJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    MutationCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "timestamp", precision: 6, nullable: false),
                    ModifierId = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    ModifierName = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    RolledBackTime = table.Column<DateTime>(type: "timestamp", precision: 6, nullable: true),
                    RolledBackGroupId = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    Status = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationMutationGroups", x => x.GroupId);
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationStoreLocks",
                columns: table => new
                {
                    LockKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LockVersion = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationStoreLocks", x => x.LockKey);
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationUnifiedVersionDocuments",
                columns: table => new
                {
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    DefinitionKey = table.Column<string>(type: "TEXT", nullable: false),
                    DefinitionIdentity = table.Column<string>(type: "TEXT", unicode: false, fixedLength: true, maxLength: 64, nullable: false, defaultValue: ""),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    Category = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    FromProject = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    SchemaVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    SchemaHash = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    EffectiveValueVersion = table.Column<long>(type: "INTEGER", nullable: true),
                    Json = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    SourceContributionsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationUnifiedVersionDocuments", x => new { x.Version, x.DefinitionKey });
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationUnifiedVersions",
                columns: table => new
                {
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    MutationGroupId = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    TriggerDefinitionKeysJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    DefinitionKeysJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    DefinitionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "timestamp", precision: 6, nullable: false),
                    ModifierId = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    ModifierName = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    Reason = table.Column<string>(type: "TEXT", nullable: true, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationUnifiedVersions", x => x.Version);
                });

            migrationBuilder.CreateTable(
                name: "ConfigurationValueHistories",
                columns: table => new
                {
                    HistoryId = table.Column<string>(type: "TEXT", nullable: false),
                    DefinitionIdentity = table.Column<string>(type: "TEXT", unicode: false, fixedLength: true, maxLength: 64, nullable: false, defaultValue: ""),
                    DefinitionKey = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    LogicalPath = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    PathDepth = table.Column<int>(type: "INTEGER", nullable: false),
                    ConfigurationPath = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    TargetKind = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    SourceProviderType = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    SourceDisplayName = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    SourcePhysicalPath = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    SourceConfigurationPath = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    MutationKind = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    Granularity = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    State = table.Column<string>(type: "TEXT", nullable: false, defaultValue: ""),
                    OldValueJson = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    NewValueJson = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    Version = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceRevisionBefore = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    SourceRevisionAfter = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    SchemaVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    SchemaHash = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    ModifiedTime = table.Column<DateTime>(type: "timestamp", precision: 6, nullable: false),
                    ModifierId = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    ModifierName = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    Reason = table.Column<string>(type: "TEXT", nullable: true, defaultValue: ""),
                    MutationGroupId = table.Column<string>(type: "TEXT", nullable: true, defaultValue: "")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigurationValueHistories", x => x.HistoryId);
                });

            migrationBuilder.InsertData(
                table: "ConfigurationStoreLocks",
                columns: new[] { "LockKey", "LockVersion" },
                values: new object[,]
                {
                    { "DefinitionPublication", 0L },
                    { "MutationGroups", 0L },
                    { "UnifiedVersions", 0L }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigDefPublisher_Publisher",
                table: "ConfigurationDefinitionPublisherStates",
                column: "PublisherIdentity");

            migrationBuilder.CreateIndex(
                name: "UX_ConfigDefPub_Identity_Revision",
                table: "ConfigurationDefinitionPublishHistories",
                columns: new[] { "DefinitionIdentity", "DefinitionRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationDefinitions_Category",
                table: "ConfigurationDefinitions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationDefinitions_FromProject",
                table: "ConfigurationDefinitions",
                column: "FromProject");

            migrationBuilder.CreateIndex(
                name: "UX_ConfigDef_Identity",
                table: "ConfigurationDefinitions",
                column: "DefinitionIdentity",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ConfigValue_Identity",
                table: "ConfigurationEffectiveValues",
                column: "DefinitionIdentity",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationMutationGroups_CreatedTime",
                table: "ConfigurationMutationGroups",
                column: "CreatedTime");

            migrationBuilder.CreateIndex(
                name: "UX_ConfigVerDoc_Identity_Version",
                table: "ConfigurationUnifiedVersionDocuments",
                columns: new[] { "DefinitionIdentity", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationUnifiedVersions_CreatedTime",
                table: "ConfigurationUnifiedVersions",
                column: "CreatedTime");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationUnifiedVersions_MutationGroupId",
                table: "ConfigurationUnifiedVersions",
                column: "MutationGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfigHist_Identity_Depth_Time",
                table: "ConfigurationValueHistories",
                columns: new[] { "DefinitionIdentity", "PathDepth", "ModifiedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_ConfigurationValueHistories_MutationGroupId",
                table: "ConfigurationValueHistories",
                column: "MutationGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfigurationDefinitionPublisherStates");

            migrationBuilder.DropTable(
                name: "ConfigurationDefinitionPublishHistories");

            migrationBuilder.DropTable(
                name: "ConfigurationDefinitions");

            migrationBuilder.DropTable(
                name: "ConfigurationEffectiveValues");

            migrationBuilder.DropTable(
                name: "ConfigurationMutationGroups");

            migrationBuilder.DropTable(
                name: "ConfigurationStoreLocks");

            migrationBuilder.DropTable(
                name: "ConfigurationUnifiedVersionDocuments");

            migrationBuilder.DropTable(
                name: "ConfigurationUnifiedVersions");

            migrationBuilder.DropTable(
                name: "ConfigurationValueHistories");
        }
    }
}
