using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheBoys.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModelLifecycleAndProviderPinning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ArchivedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelProjects_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GenerationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Provider = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ProviderModel = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    ApiVersion = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    PolicyVersion = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    PricingVersion = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    AccountScope = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    InputStorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InputHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    InputConsentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedHeightMm = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    ShouldTexture = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestFingerprint = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SourceVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenerationJobs", x => x.Id);
                    table.CheckConstraint("CK_GenerationJobs_Completion", "[CompletedAtUtc] IS NULL OR [CompletedAtUtc] >= [CreatedAtUtc]");
                    table.CheckConstraint("CK_GenerationJobs_Operation", "[Operation] IN ('ImageTo3D','TextTo3D','Resize','Repair')");
                    table.CheckConstraint("CK_GenerationJobs_RequestedHeight", "[RequestedHeightMm] > 0");
                    table.CheckConstraint("CK_GenerationJobs_Status", "[Status] IN ('Queued','Processing','Succeeded','Failed','NeedsAttention','Cancelled')");
                    table.ForeignKey(
                        name: "FK_GenerationJobs_ConsentRecords_InputConsentId",
                        column: x => x.InputConsentId,
                        principalTable: "ConsentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GenerationJobs_ModelProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "ModelProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GenerationJobs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GenerationStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Stage = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    AttemptNo = table.Column<int>(type: "int", nullable: false),
                    Provider = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ProviderAccountScope = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    ProviderTaskId = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Progress = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SubmissionPending = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PollFailureCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    NextPollAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    LeaseOwner = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ProviderCredits = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    ActualCostVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ErrorCode = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenerationStages", x => x.Id);
                    table.CheckConstraint("CK_GenerationStages_ActualCostVnd", "[ActualCostVnd] IS NULL OR [ActualCostVnd] >= 0");
                    table.CheckConstraint("CK_GenerationStages_AttemptNo", "[AttemptNo] > 0");
                    table.CheckConstraint("CK_GenerationStages_PollFailureCount", "[PollFailureCount] >= 0");
                    table.CheckConstraint("CK_GenerationStages_ProviderCredits", "[ProviderCredits] IS NULL OR [ProviderCredits] >= 0");
                    table.CheckConstraint("CK_GenerationStages_Stage", "[Stage] IN ('Generate','Resize','Convert','Analyze','Repair')");
                    table.CheckConstraint("CK_GenerationStages_Status", "[Status] IN ('Queued','Submitting','Running','Succeeded','Failed','NeedsAttention')");
                    table.ForeignKey(
                        name: "FK_GenerationStages_GenerationJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "GenerationJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ParentVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VersionNo = table.Column<int>(type: "int", nullable: false),
                    GeometryHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    WidthMm = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    DepthMm = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    HeightMm = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    VolumeCm3 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    IsWatertight = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    PrintabilityStatus = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    AnalysisCoverage = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    AnalysisJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelVersions", x => x.Id);
                    table.CheckConstraint("CK_ModelVersions_AnalysisCoverage", "[AnalysisCoverage] IN ('GeometryOnly','GeometryAndThickness','ManualSlicer')");
                    table.CheckConstraint("CK_ModelVersions_DepthMm", "[DepthMm] > 0");
                    table.CheckConstraint("CK_ModelVersions_HeightMm", "[HeightMm] > 0");
                    table.CheckConstraint("CK_ModelVersions_VersionNo", "[VersionNo] > 0");
                    table.CheckConstraint("CK_ModelVersions_VolumeCm3", "[VolumeCm3] IS NULL OR [VolumeCm3] > 0");
                    table.CheckConstraint("CK_ModelVersions_WidthMm", "[WidthMm] > 0");
                    table.ForeignKey(
                        name: "FK_ModelVersions_GenerationJobs_SourceJobId",
                        column: x => x.SourceJobId,
                        principalTable: "GenerationJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelVersions_ModelProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "ModelProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModelVersions_ModelVersions_ParentVersionId",
                        column: x => x.ParentVersionId,
                        principalTable: "ModelVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModelAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Format = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Purpose = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Sha256 = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    ByteLength = table.Column<long>(type: "bigint", nullable: false),
                    CoordinateUnit = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    UpAxis = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    PurgedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    RetainUntilUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    LegalHold = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelAssets", x => x.Id);
                    table.CheckConstraint("CK_ModelAssets_ByteLength", "[ByteLength] > 0");
                    table.CheckConstraint("CK_ModelAssets_CoordinateUnit", "[CoordinateUnit] IN ('mm','m','pixel')");
                    table.CheckConstraint("CK_ModelAssets_Format", "[Format] IN ('GLB','STL','PNG','JPEG','WEBP','3MF')");
                    table.CheckConstraint("CK_ModelAssets_Purpose", "[Purpose] IN ('Preview','Print','Thumbnail')");
                    table.CheckConstraint("CK_ModelAssets_UpAxis", "[UpAxis] IS NULL OR [UpAxis] IN ('X','Y','Z')");
                    table.ForeignKey(
                        name: "FK_ModelAssets_ModelVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "ModelVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GenerationJobs_InputConsentId",
                table: "GenerationJobs",
                column: "InputConsentId");

            migrationBuilder.CreateIndex(
                name: "IX_GenerationJobs_ProjectId",
                table: "GenerationJobs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_GenerationJobs_SourceVersionId",
                table: "GenerationJobs",
                column: "SourceVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_GenerationJobs_Status_CreatedAtUtc",
                table: "GenerationJobs",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GenerationJobs_UserId",
                table: "GenerationJobs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_GenerationJobs_User_ClientRequestId",
                table: "GenerationJobs",
                columns: new[] { "UserId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GenerationStages_JobId",
                table: "GenerationStages",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_Stages_Queue",
                table: "GenerationStages",
                columns: new[] { "Status", "NextPollAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_GenerationStages_Job_Stage_Attempt",
                table: "GenerationStages",
                columns: new[] { "JobId", "Stage", "AttemptNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Stages_Active",
                table: "GenerationStages",
                columns: new[] { "JobId", "Stage" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "UX_Stages_ProviderTask",
                table: "GenerationStages",
                columns: new[] { "Provider", "ProviderAccountScope", "ProviderTaskId" },
                unique: true,
                filter: "[ProviderTaskId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ModelAssets_VersionId",
                table: "ModelAssets",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "UX_ModelAssets_StorageKey",
                table: "ModelAssets",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ModelAssets_Version_Format_Purpose",
                table: "ModelAssets",
                columns: new[] { "VersionId", "Format", "Purpose" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelProjects_UserId",
                table: "ModelProjects",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_ModelProjects_Id_UserId",
                table: "ModelProjects",
                columns: new[] { "Id", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_ParentVersionId",
                table: "ModelVersions",
                column: "ParentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_ProjectId",
                table: "ModelVersions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelVersions_SourceJobId",
                table: "ModelVersions",
                column: "SourceJobId");

            migrationBuilder.CreateIndex(
                name: "UX_ModelVersions_Project_VersionNo",
                table: "ModelVersions",
                columns: new[] { "ProjectId", "VersionNo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_GenerationJobs_ModelVersions_SourceVersionId",
                table: "GenerationJobs",
                column: "SourceVersionId",
                principalTable: "ModelVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'GenerationTasks')
BEGIN
    INSERT INTO ModelProjects (Id, UserId, Name, CreatedAtUtc)
    SELECT 
        NEWID(), 
        gt.UserId, 
        N'Default Project', 
        MIN(gt.CreatedAtUtc)
    FROM GenerationTasks gt
    WHERE NOT EXISTS (
        SELECT 1 FROM ModelProjects mp WHERE mp.UserId = gt.UserId
    )
    GROUP BY gt.UserId;

    INSERT INTO GenerationJobs (
        Id, ProjectId, UserId, Operation, Provider, ProviderModel,
        ApiVersion, PolicyVersion, PricingVersion, AccountScope,
        Prompt, InputStorageKey, InputHash, RequestedHeightMm, ShouldTexture,
        Status, ClientRequestId, RequestFingerprint, ErrorMessage,
        CreatedAtUtc, CompletedAtUtc
    )
    SELECT 
        gt.Id,
        mp.Id AS ProjectId,
        gt.UserId,
        'ImageTo3D' AS Operation,
        ISNULL(NULLIF(gt.Provider, ''), 'Meshy') AS Provider,
        ISNULL(NULLIF(gt.AiModel, ''), 'meshy-6-lite') AS ProviderModel,
        CASE WHEN gt.Provider = 'Tripo' THEN 'v3' ELSE 'v1' END AS ApiVersion,
        'v1.0' AS PolicyVersion,
        '2026.01' AS PricingVersion,
        'default' AS AccountScope,
        gt.Prompt,
        gt.InputImageUrl AS InputStorageKey,
        HASHBYTES('SHA2_256', ISNULL(gt.InputImageUrl, CAST(gt.Id AS varchar(36)))) AS InputHash,
        CAST(gt.RequestedHeightMm AS decimal(10,3)) AS RequestedHeightMm,
        gt.ShouldTexture,
        CASE 
            WHEN gt.Status = 0 THEN 'Queued'
            WHEN gt.Status = 1 THEN 'Processing'
            WHEN gt.Status = 2 THEN 'Processing'
            WHEN gt.Status = 3 THEN 'Succeeded'
            WHEN gt.Status = 4 THEN 'Failed'
            WHEN gt.Status = 5 THEN 'NeedsAttention'
            ELSE 'Failed'
        END AS Status,
        TRY_CONVERT(uniqueidentifier, gt.ClientRequestId) AS ClientRequestId,
        HASHBYTES('SHA2_256', ISNULL(gt.RequestFingerprint, CAST(gt.Id AS varchar(36)))) AS RequestFingerprint,
        gt.ErrorMessage,
        gt.CreatedAtUtc,
        gt.CompletedAtUtc
    FROM GenerationTasks gt
    CROSS APPLY (
        SELECT TOP 1 Id FROM ModelProjects mp WHERE mp.UserId = gt.UserId ORDER BY mp.CreatedAtUtc ASC
    ) mp
    WHERE NOT EXISTS (SELECT 1 FROM GenerationJobs gj WHERE gj.Id = gt.Id)
      AND TRY_CONVERT(uniqueidentifier, gt.ClientRequestId) IS NOT NULL;

    INSERT INTO GenerationStages (
        Id, JobId, Stage, AttemptNo, Provider, ProviderAccountScope,
        ProviderTaskId, Status, Progress, IsActive, SubmissionPending,
        PollFailureCount, NextPollAtUtc, CreatedAtUtc, CompletedAtUtc
    )
    SELECT 
        NEWID(),
        gt.Id AS JobId,
        CASE 
            WHEN gt.Stage = 0 THEN 'Generate'
            WHEN gt.Stage = 1 THEN 'Resize'
            WHEN gt.Stage = 2 THEN 'Convert'
            WHEN gt.Stage = 3 THEN 'Analyze'
            ELSE 'Generate'
        END AS Stage,
        1 AS AttemptNo,
        ISNULL(NULLIF(gt.Provider, ''), 'Meshy') AS Provider,
        'default' AS ProviderAccountScope,
        gt.ProviderTaskId,
        CASE 
            WHEN gt.Status = 3 THEN 'Succeeded'
            WHEN gt.Status = 4 THEN 'Failed'
            WHEN gt.Status = 5 THEN 'NeedsAttention'
            ELSE 'Running'
        END AS Status,
        gt.Progress,
        1 AS IsActive,
        gt.SubmissionPending,
        0 AS PollFailureCount,
        gt.NextPollAtUtc,
        gt.CreatedAtUtc,
        gt.CompletedAtUtc
    FROM GenerationTasks gt
    INNER JOIN GenerationJobs gj ON gj.Id = gt.Id
    WHERE NOT EXISTS (SELECT 1 FROM GenerationStages gs WHERE gs.JobId = gt.Id);

    INSERT INTO ModelVersions (
        Id, ProjectId, SourceJobId, VersionNo, GeometryHash,
        WidthMm, DepthMm, HeightMm, VolumeCm3, PrintabilityStatus,
        AnalysisCoverage, AnalysisJson, IsPublished, PublishedAtUtc, CreatedAtUtc
    )
    SELECT 
        NEWID(),
        gj.ProjectId,
        gt.Id AS SourceJobId,
        1 AS VersionNo,
        HASHBYTES('SHA2_256', ISNULL(gt.StoragePathStl, CAST(gt.Id AS varchar(36)))) AS GeometryHash,
        ISNULL(gt.BoundingBoxX_Mm, 100),
        ISNULL(gt.BoundingBoxY_Mm, 100),
        ISNULL(gt.BoundingBoxZ_Mm, gt.RequestedHeightMm),
        gt.EstimatedVolumeCm3,
        ISNULL(gt.PrintabilityStatus, 'healthy'),
        'GeometryAndThickness',
        gt.PrintabilityJson,
        1 AS IsPublished,
        ISNULL(gt.CompletedAtUtc, SYSUTCDATETIME()),
        ISNULL(gt.CompletedAtUtc, SYSUTCDATETIME())
    FROM GenerationTasks gt
    INNER JOIN GenerationJobs gj ON gj.Id = gt.Id
    WHERE gt.Status = 3
      AND NOT EXISTS (SELECT 1 FROM ModelVersions mv WHERE mv.SourceJobId = gt.Id);

    INSERT INTO ModelAssets (
        Id, VersionId, Format, Purpose, StorageKey, Sha256,
        ByteLength, CoordinateUnit, UpAxis, CreatedAtUtc
    )
    SELECT 
        NEWID(),
        mv.Id AS VersionId,
        'GLB' AS Format,
        'Preview' AS Purpose,
        gt.StoragePathGlb,
        mv.GeometryHash,
        1024 AS ByteLength,
        'm' AS CoordinateUnit,
        'Y' AS UpAxis,
        mv.CreatedAtUtc
    FROM ModelVersions mv
    INNER JOIN GenerationTasks gt ON gt.Id = mv.SourceJobId
    WHERE gt.StoragePathGlb IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM ModelAssets ma WHERE ma.VersionId = mv.Id AND ma.Format = 'GLB');

    INSERT INTO ModelAssets (
        Id, VersionId, Format, Purpose, StorageKey, Sha256,
        ByteLength, CoordinateUnit, UpAxis, CreatedAtUtc
    )
    SELECT 
        NEWID(),
        mv.Id AS VersionId,
        'STL' AS Format,
        'Print' AS Purpose,
        gt.StoragePathStl,
        mv.GeometryHash,
        1024 AS ByteLength,
        'mm' AS CoordinateUnit,
        'Z' AS UpAxis,
        mv.CreatedAtUtc
    FROM ModelVersions mv
    INNER JOIN GenerationTasks gt ON gt.Id = mv.SourceJobId
    WHERE gt.StoragePathStl IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM ModelAssets ma WHERE ma.VersionId = mv.Id AND ma.Format = 'STL');
END;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GenerationJobs_ModelProjects_ProjectId",
                table: "GenerationJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_ModelVersions_ModelProjects_ProjectId",
                table: "ModelVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_GenerationJobs_ModelVersions_SourceVersionId",
                table: "GenerationJobs");

            migrationBuilder.DropTable(
                name: "GenerationStages");

            migrationBuilder.DropTable(
                name: "ModelAssets");

            migrationBuilder.DropTable(
                name: "ModelProjects");

            migrationBuilder.DropTable(
                name: "ModelVersions");

            migrationBuilder.DropTable(
                name: "GenerationJobs");
        }
    }
}
