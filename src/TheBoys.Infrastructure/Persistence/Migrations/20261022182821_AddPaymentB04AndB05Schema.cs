using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheBoys.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentB04AndB05Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Gateway = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    MerchantReference = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    ProviderTransactionId = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    AmountVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "varchar(3)", unicode: false, maxLength: 3, nullable: false, defaultValue: "VND"),
                    GatewayFeeActualVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CheckoutUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ClientRequestId = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    RequestFingerprint = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAttempts", x => x.Id);
                    table.CheckConstraint("CK_PaymentAttempts_AmountVnd", "[AmountVnd] > 0");
                    table.CheckConstraint("CK_PaymentAttempts_Currency", "[Currency] = 'VND'");
                    table.CheckConstraint("CK_PaymentAttempts_GatewayFee", "[GatewayFeeActualVnd] IS NULL OR [GatewayFeeActualVnd] >= 0");
                    table.CheckConstraint("CK_PaymentAttempts_Status", "[Status] IN ('Pending','Succeeded','Failed','Expired','NeedsAttention')");
                    table.ForeignKey(
                        name: "FK_PaymentAttempts_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Gateway = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    EventId = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    PayloadHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ProcessingResult = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEvents", x => x.Id);
                    table.CheckConstraint("CK_PaymentEvents_ProcessingResult", "[ProcessingResult] IN ('Pending','Applied','Ignored','NeedsAttention')");
                    table.ForeignKey(
                        name: "FK_PaymentEvents_PaymentAttempts_PaymentAttemptId",
                        column: x => x.PaymentAttemptId,
                        principalTable: "PaymentAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_OrderId_CreatedAtUtc",
                table: "PaymentAttempts",
                columns: new[] { "OrderId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAttempts_Status_ExpiresAtUtc",
                table: "PaymentAttempts",
                columns: new[] { "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PaymentAttempts_Gateway_MerchantReference",
                table: "PaymentAttempts",
                columns: new[] { "Gateway", "MerchantReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PaymentAttempts_Gateway_ProviderTransaction",
                table: "PaymentAttempts",
                columns: new[] { "Gateway", "ProviderTransactionId" },
                unique: true,
                filter: "[ProviderTransactionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEvents_PaymentAttempt_ReceivedAtUtc",
                table: "PaymentEvents",
                columns: new[] { "PaymentAttemptId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_PaymentEvents_Gateway_EventId",
                table: "PaymentEvents",
                columns: new[] { "Gateway", "EventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentEvents");

            migrationBuilder.DropTable(
                name: "PaymentAttempts");
        }
    }
}
