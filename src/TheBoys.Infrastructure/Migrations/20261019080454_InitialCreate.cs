using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheBoys.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PricingPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackageCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PriceVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PartnerBaseCostVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 120000.00m),
                    Material = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HeightRangeCm = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingPackages", x => x.Id);
                    table.CheckConstraint("CK_PricingPackages_Metrics", "[PriceVnd] > 0 AND [PartnerBaseCostVnd] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PhoneNumber = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Role = table.Column<int>(type: "int", nullable: false),
                    DeviceFingerprint = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    IsPhoneVerified = table.Column<bool>(type: "bit", nullable: false),
                    FreeTrialRemaining = table.Column<int>(type: "int", nullable: false, defaultValue: 2),
                    PurchasedRemaining = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    TotalUsed = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_CreditsNonNegative", "[FreeTrialRemaining] >= 0 AND [PurchasedRemaining] >= 0 AND [TotalUsed] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "GenerationTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskType = table.Column<int>(type: "int", nullable: false),
                    Prompt = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ArtStyle = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InputImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CleanedImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProviderTaskId = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    DurationSeconds = table.Column<int>(type: "int", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StoragePathGlb = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StoragePathStl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Polycount = table.Column<int>(type: "int", nullable: true),
                    VertexCount = table.Column<int>(type: "int", nullable: true),
                    IsWatertight = table.Column<bool>(type: "bit", nullable: false),
                    MinWallThicknessMm = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    BoundingBoxX_Mm = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    BoundingBoxY_Mm = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    BoundingBoxZ_Mm = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    EstimatedVolumeCm3 = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    IsApprovedForPrinting = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenerationTasks", x => x.Id);
                    table.CheckConstraint("CK_GenerationTasks_Metrics", "([Polycount] IS NULL OR [Polycount] >= 0) AND ([VertexCount] IS NULL OR [VertexCount] >= 0)");
                    table.ForeignKey(
                        name: "FK_GenerationTasks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PartnerLabs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LabName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactPerson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CapacityPerDay = table.Column<int>(type: "int", nullable: false, defaultValue: 20),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerLabs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartnerLabs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GenerationTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PricingPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PartnerLabId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UnitPriceSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PartnerCostSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AiCostBudgetSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefectBudgetSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ShippingSubsidySnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentGatewayFeeSnapshot = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    SubTotalVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ShippingFeeRaw = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FinalShippingFee = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CustomEngravingText = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReceiverName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ReceiverPhone = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    StreetAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    District = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "Thành phố Hồ Chí Minh"),
                    CustomerNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TrackingNumber = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    DefectReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EvidencePhotoUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EstimatedDefectCostVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true, defaultValue: 6000.00m),
                    ReprintApproved = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    SentToPartnerAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    PrintingStartedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    PackagedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ShippedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    StatusNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Orders_Financials", "[SubTotalVnd] >= 0 AND [TotalAmount] >= 0 AND [Quantity] > 0");
                    table.ForeignKey(
                        name: "FK_Orders_GenerationTasks_GenerationTaskId",
                        column: x => x.GenerationTaskId,
                        principalTable: "GenerationTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_PartnerLabs_PartnerLabId",
                        column: x => x.PartnerLabId,
                        principalTable: "PartnerLabs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_PricingPackages_PricingPackageId",
                        column: x => x.PricingPackageId,
                        principalTable: "PricingPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentMethod = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    TransactionReference = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    GatewayResponseJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payments_PositiveAmount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_Payments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GenerationTasks_User_Library_ESR",
                table: "GenerationTasks",
                columns: new[] { "UserId", "IsApprovedForPrinting", "CreatedAtUtc" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_GenerationTasks_User_Status_ESR",
                table: "GenerationTasks",
                columns: new[] { "UserId", "Status", "CreatedAtUtc" })
                .Annotation("SqlServer:Include", new[] { "TaskType", "ProviderTaskId" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_GenerationTaskId",
                table: "Orders",
                column: "GenerationTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Partner_Queue_ESR",
                table: "Orders",
                columns: new[] { "PartnerLabId", "Status", "CreatedAtUtc" },
                filter: "[PartnerLabId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PricingPackageId",
                table: "Orders",
                column: "PricingPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_User_Status_ESR",
                table: "Orders",
                columns: new[] { "UserId", "Status", "CreatedAtUtc" })
                .Annotation("SqlServer:Include", new[] { "OrderCode", "TotalAmount" });

            migrationBuilder.CreateIndex(
                name: "UX_Orders_OrderCode",
                table: "Orders",
                column: "OrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartnerLabs_UserId",
                table: "PartnerLabs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Order_Status",
                table: "Payments",
                columns: new[] { "OrderId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_Payments_TransactionRef",
                table: "Payments",
                column: "TransactionReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_PricingPackages_Code",
                table: "PricingPackages",
                column: "PackageCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_DeviceFingerprint",
                table: "Users",
                column: "DeviceFingerprint",
                filter: "[DeviceFingerprint] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UX_Users_Email_Active",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "GenerationTasks");

            migrationBuilder.DropTable(
                name: "PartnerLabs");

            migrationBuilder.DropTable(
                name: "PricingPackages");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
