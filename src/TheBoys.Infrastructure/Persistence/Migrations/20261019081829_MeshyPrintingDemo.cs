using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheBoys.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MeshyPrintingDemo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxHeightMm",
                table: "PricingPackages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MinHeightMm",
                table: "PricingPackages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ClientRequestId",
                table: "Orders",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DepthMmSnapshot",
                table: "Orders",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "HeightMmSnapshot",
                table: "Orders",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "MaterialSnapshot",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "PrintHeightMm",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                table: "Orders",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "WidthMmSnapshot",
                table: "Orders",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedVolumeCm3",
                table: "GenerationTasks",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(8,2)",
                oldPrecision: 8,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiModel",
                table: "GenerationTasks",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AnalysisTaskId",
                table: "GenerationTasks",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ChargedPurchasedCredit",
                table: "GenerationTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ClientRequestId",
                table: "GenerationTasks",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ConvertTaskId",
                table: "GenerationTasks",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CreditRefunded",
                table: "GenerationTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextPollAtUtc",
                table: "GenerationTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrintabilityJson",
                table: "GenerationTasks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrintabilityStatus",
                table: "GenerationTasks",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrinterType",
                table: "GenerationTasks",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Progress",
                table: "GenerationTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "GenerationTasks",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                table: "GenerationTasks",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RequestedHeightMm",
                table: "GenerationTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ResizeTaskId",
                table: "GenerationTasks",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResizedModelUrl",
                table: "GenerationTasks",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "GenerationTasks",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewStatus",
                table: "GenerationTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAtUtc",
                table: "GenerationTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "GenerationTasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShouldTexture",
                table: "GenerationTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Stage",
                table: "GenerationTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SubmissionPending",
                table: "GenerationTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE Users SET Role = 2 WHERE Role = 1;
                UPDATE PricingPackages SET MinHeightMm = 100, MaxHeightMm = 120
                    WHERE PackageCode IN ('TIER_STANDARD_390', 'TIER_BASELINE_300');
                UPDATE Orders SET ClientRequestId = REPLACE(CONVERT(nvarchar(36), Id), '-', '');
                UPDATE GenerationTasks SET ClientRequestId = REPLACE(CONVERT(nvarchar(36), Id), '-', ''),
                    Provider = 'Meshy', AiModel = 'legacy', PrinterType = 'sla', RequestedHeightMm = 100,
                    Stage = CASE WHEN Status = 3 THEN 4 ELSE 0 END,
                    ReviewStatus = CASE WHEN IsApprovedForPrinting = 1 THEN 1 ELSE 0 END;
                UPDATE GenerationTasks SET Status = 6,
                    ErrorMessage = N'Task cũ cần Staff đối soát trước khi tiếp tục tích hợp Meshy mới.'
                    WHERE Status IN (0, 1, 2);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId_ClientRequestId",
                table: "Orders",
                columns: new[] { "UserId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GenerationTasks_Status_NextPollAtUtc",
                table: "GenerationTasks",
                columns: new[] { "Status", "NextPollAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GenerationTasks_UserId_ClientRequestId",
                table: "GenerationTasks",
                columns: new[] { "UserId", "ClientRequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId_ClientRequestId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_GenerationTasks_Status_NextPollAtUtc",
                table: "GenerationTasks");

            migrationBuilder.DropIndex(
                name: "IX_GenerationTasks_UserId_ClientRequestId",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "MaxHeightMm",
                table: "PricingPackages");

            migrationBuilder.DropColumn(
                name: "MinHeightMm",
                table: "PricingPackages");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DepthMmSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "HeightMmSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "MaterialSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PrintHeightMm",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "WidthMmSnapshot",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AiModel",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "AnalysisTaskId",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "ChargedPurchasedCredit",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "ConvertTaskId",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "CreditRefunded",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "NextPollAtUtc",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "PrintabilityJson",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "PrintabilityStatus",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "PrinterType",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "Progress",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "Provider",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "RequestedHeightMm",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "ResizeTaskId",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "ResizedModelUrl",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "ShouldTexture",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "GenerationTasks");

            migrationBuilder.DropColumn(
                name: "SubmissionPending",
                table: "GenerationTasks");

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedVolumeCm3",
                table: "GenerationTasks",
                type: "decimal(8,2)",
                precision: 8,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldNullable: true);
        }
    }
}
