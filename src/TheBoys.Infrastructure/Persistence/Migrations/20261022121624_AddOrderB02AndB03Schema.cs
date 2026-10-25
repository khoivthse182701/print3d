using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheBoys.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderB02AndB03Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "GenerationTaskId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "AddressSnapshotJson",
                table: "Orders",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancellationRequestedAtUtc",
                table: "Orders",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Orders",
                type: "varchar(3)",
                unicode: false,
                maxLength: 3,
                nullable: false,
                defaultValue: "VND");

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentStatus",
                table: "Orders",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: false,
                defaultValue: "PendingPayment");

            migrationBuilder.AddColumn<string>(
                name: "OrderPaymentStatus",
                table: "Orders",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: false,
                defaultValue: "Unpaid");

            migrationBuilder.AddColumn<string>(
                name: "PolicyVersion",
                table: "Orders",
                type: "varchar(40)",
                unicode: false,
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "QuoteId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CustomerApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    ConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PolicyVersion = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerApprovals_ModelVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "ModelVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerApprovals_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    AssetHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPriceVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaterialSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HeightMm = table.Column<decimal>(type: "decimal(10,3)", precision: 10, scale: 3, nullable: false),
                    EngravingText = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PartnerUnitBudgetVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.CheckConstraint("CK_OrderItems_HeightMm", "[HeightMm] > 0");
                    table.CheckConstraint("CK_OrderItems_LineNumber", "[LineNumber] > 0");
                    table.CheckConstraint("CK_OrderItems_PartnerBudget", "[PartnerUnitBudgetVnd] >= 0");
                    table.CheckConstraint("CK_OrderItems_Quantity", "[Quantity] >= 1 AND [Quantity] <= 10");
                    table.CheckConstraint("CK_OrderItems_UnitPrice", "[UnitPriceVnd] > 0");
                    table.ForeignKey(
                        name: "FK_OrderItems_ModelVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "ModelVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderStatusHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Axis = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    FromStatus = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Source = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OperationKey = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderStatusHistories", x => x.Id);
                    table.CheckConstraint("CK_OrderStatusHistories_Axis", "[Axis] IN ('Fulfillment', 'Payment')");
                    table.ForeignKey(
                        name: "FK_OrderStatusHistories_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderStatusHistories_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrintReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ChecklistJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceStorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintReviews", x => x.Id);
                    table.CheckConstraint("CK_PrintReviews_Decision", "[Decision] IN ('Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_PrintReviews_ModelVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "ModelVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrintReviews_Users_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Quotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfigurationHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    ApprovalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PricingPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPriceVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ShippingFeeVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AddressJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                    table.CheckConstraint("CK_Quotes_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.CheckConstraint("CK_Quotes_Financials", "[UnitPriceVnd] > 0 AND [ShippingFeeVnd] >= 0 AND [TotalVnd] = [UnitPriceVnd] * [Quantity] + [ShippingFeeVnd]");
                    table.CheckConstraint("CK_Quotes_Quantity", "[Quantity] >= 1 AND [Quantity] <= 10");
                    table.ForeignKey(
                        name: "FK_Quotes_CustomerApprovals_ApprovalId",
                        column: x => x.ApprovalId,
                        principalTable: "CustomerApprovals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Quotes_ModelVersions_VersionId",
                        column: x => x.VersionId,
                        principalTable: "ModelVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Quotes_PricingPackages_PricingPackageId",
                        column: x => x.PricingPackageId,
                        principalTable: "PricingPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Quotes_PrintReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "PrintReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Quotes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Orders_QuoteId",
                table: "Orders",
                column: "QuoteId",
                unique: true,
                filter: "[QuoteId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerApprovals_User_Version_Hash",
                table: "CustomerApprovals",
                columns: new[] { "UserId", "VersionId", "ConfigurationHash", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerApprovals_VersionId",
                table: "CustomerApprovals",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_VersionId",
                table: "OrderItems",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "UX_OrderItems_OrderId_LineNumber",
                table: "OrderItems",
                columns: new[] { "OrderId", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistories_ActorUserId",
                table: "OrderStatusHistories",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderStatusHistories_OrderId_CreatedAtUtc",
                table: "OrderStatusHistories",
                columns: new[] { "OrderId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_OrderStatusHistories_OrderId_OperationKey",
                table: "OrderStatusHistories",
                columns: new[] { "OrderId", "OperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrintReviews_ReviewerUserId",
                table: "PrintReviews",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PrintReviews_Version_ConfigHash",
                table: "PrintReviews",
                columns: new[] { "VersionId", "ConfigurationHash", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_ApprovalId",
                table: "Quotes",
                column: "ApprovalId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_ExpiresAtUtc",
                table: "Quotes",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_PricingPackageId",
                table: "Quotes",
                column: "PricingPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_ReviewId",
                table: "Quotes",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_UserId_CreatedAtUtc",
                table: "Quotes",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_Version_ConfigHash",
                table: "Quotes",
                columns: new[] { "VersionId", "ConfigurationHash" });

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Quotes_QuoteId",
                table: "Orders",
                column: "QuoteId",
                principalTable: "Quotes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Quotes_QuoteId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "OrderStatusHistories");

            migrationBuilder.DropTable(
                name: "Quotes");

            migrationBuilder.DropTable(
                name: "CustomerApprovals");

            migrationBuilder.DropTable(
                name: "PrintReviews");

            migrationBuilder.DropIndex(
                name: "UX_Orders_QuoteId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AddressSnapshotJson",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CancellationRequestedAtUtc",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "FulfillmentStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderPaymentStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PolicyVersion",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "QuoteId",
                table: "Orders");

            migrationBuilder.AlterColumn<Guid>(
                name: "GenerationTaskId",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
