using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheBoys.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpsB06AndB07FulfillmentAndDefects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartnerAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerLabId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PartnerCostSnapshotVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    DecisionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerAssignments", x => x.Id);
                    table.CheckConstraint("CK_PartnerAssignments_ActiveState", "(([Status] IN ('Offered','Accepted') AND [IsActive] = 1) OR ([Status] NOT IN ('Offered','Accepted') AND [IsActive] = 0))");
                    table.CheckConstraint("CK_PartnerAssignments_Cost", "[PartnerCostSnapshotVnd] >= 0");
                    table.CheckConstraint("CK_PartnerAssignments_Status", "[Status] IN ('Offered','Accepted','Rejected','Expired','Completed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_PartnerAssignments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PartnerAssignments_PartnerLabs_PartnerLabId",
                        column: x => x.PartnerLabId,
                        principalTable: "PartnerLabs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PartnerAssignments_Users_AssignedByUserId",
                        column: x => x.AssignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Shipments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Carrier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TrackingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ShippingActualVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    DeliveryEvidenceStorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.Id);
                    table.CheckConstraint("CK_Shipments_DeliveredEvidence", "([Status] <> 'Delivered' OR ([DeliveredAtUtc] IS NOT NULL AND [DeliveryEvidenceStorageKey] IS NOT NULL))");
                    table.CheckConstraint("CK_Shipments_Status", "[Status] IN ('Created','Shipping','DeliveryFailed','Delivered','Returned','Cancelled')");
                    table.ForeignKey(
                        name: "FK_Shipments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunNo = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ActualCostVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    QcEvidenceStorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    QcNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionRuns", x => x.Id);
                    table.CheckConstraint("CK_ProductionRuns_Metrics", "[RunNo] > 0 AND [Quantity] > 0 AND ([ActualCostVnd] IS NULL OR [ActualCostVnd] >= 0)");
                    table.CheckConstraint("CK_ProductionRuns_QcPassed", "([Status] <> 'Passed' OR ([QcEvidenceStorageKey] IS NOT NULL AND [QcNote] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL))");
                    table.CheckConstraint("CK_ProductionRuns_Status", "[Status] IN ('Printing','PostProcessing','QualityCheck','Passed','Failed','Cancelled')");
                    table.ForeignKey(
                        name: "FK_ProductionRuns_PartnerAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "PartnerAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventKey = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentEvents_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShipmentEvents_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DefectReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ResolutionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RefundId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReprintRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefectReports", x => x.Id);
                    table.CheckConstraint("CK_DefectReports_SingleResolution", "([RefundId] IS NULL OR [ReprintRunId] IS NULL)");
                    table.CheckConstraint("CK_DefectReports_Status", "[Status] IN ('Open','AwaitingEvidence','Investigating','ReprintApproved','RefundApproved','Rejected','Resolved')");
                    table.ForeignKey(
                        name: "FK_DefectReports_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DefectReports_ProductionRuns_ReprintRunId",
                        column: x => x.ReprintRunId,
                        principalTable: "ProductionRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DefectReports_Refunds_RefundId",
                        column: x => x.RefundId,
                        principalTable: "Refunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DefectReports_Users_ResolvedByUserId",
                        column: x => x.ResolvedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DefectReports_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DefectEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Sha256 = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefectEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DefectEvidence_DefectReports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "DefectReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DefectEvidence_ReportId",
                table: "DefectEvidence",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_DefectEvidence_StorageKey",
                table: "DefectEvidence",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DefectReports_OrderId",
                table: "DefectReports",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_DefectReports_RefundId",
                table: "DefectReports",
                column: "RefundId");

            migrationBuilder.CreateIndex(
                name: "IX_DefectReports_ReprintRunId",
                table: "DefectReports",
                column: "ReprintRunId",
                unique: true,
                filter: "[ReprintRunId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DefectReports_ResolvedByUserId",
                table: "DefectReports",
                column: "ResolvedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DefectReports_UserId_ClientRequestId",
                table: "DefectReports",
                columns: new[] { "UserId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerAssignments_AssignedByUserId",
                table: "PartnerAssignments",
                column: "AssignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerAssignments_PartnerLabId",
                table: "PartnerAssignments",
                column: "PartnerLabId");

            migrationBuilder.CreateIndex(
                name: "UX_Assignment_ActiveOrder",
                table: "PartnerAssignments",
                column: "OrderId",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionRuns_AssignmentId_RunNo",
                table: "ProductionRuns",
                columns: new[] { "AssignmentId", "RunNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentEvents_ActorUserId",
                table: "ShipmentEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentEvents_ShipmentId_EventKey",
                table: "ShipmentEvents",
                columns: new[] { "ShipmentId", "EventKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_Carrier_TrackingNumber",
                table: "Shipments",
                columns: new[] { "Carrier", "TrackingNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_OrderId",
                table: "Shipments",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DefectEvidence");

            migrationBuilder.DropTable(
                name: "ShipmentEvents");

            migrationBuilder.DropTable(
                name: "DefectReports");

            migrationBuilder.DropTable(
                name: "Shipments");

            migrationBuilder.DropTable(
                name: "ProductionRuns");

            migrationBuilder.DropTable(
                name: "PartnerAssignments");
        }
    }
}
