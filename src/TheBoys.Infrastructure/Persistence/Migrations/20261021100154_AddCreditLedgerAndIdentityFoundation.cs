using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheBoys.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditLedgerAndIdentityFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountStatus",
                table: "Users",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "Unverified");

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerifiedAtUtc",
                table: "Users",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginAttempts",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEndUtc",
                table: "Users",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "Users",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
UPDATE [Users]
SET [NormalizedEmail] = UPPER(LTRIM(RTRIM([Email]))),
    [AccountStatus] = 'Active',
    [EmailVerifiedAtUtc] = SYSUTCDATETIME()
WHERE [NormalizedEmail] = '' OR [NormalizedEmail] IS NULL;
");

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "ThÃƒÂ nh phÃ¡Â»â€˜ HÃ¡Â»â€œ ChÃƒÂ­ Minh",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldDefaultValue: "ThÃ nh phá»‘ Há»“ ChÃ­ Minh");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    AggregateType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TraceId = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    RedactedChangesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.CheckConstraint("CK_AuditLogs_JsonValid", "ISJSON([RedactedChangesJson]) = 1");
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConsentRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    PolicyVersion = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false),
                    Granted = table.Column<bool>(type: "bit", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsentRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditAccounts",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrialBalance = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    PurchasedBalance = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditAccounts", x => x.UserId);
                    table.CheckConstraint("CK_CreditAccounts_PurchasedBalanceNonNegative", "[PurchasedBalance] >= 0");
                    table.CheckConstraint("CK_CreditAccounts_TrialBalanceNonNegative", "[TrialBalance] >= 0");
                    table.ForeignKey(
                        name: "FK_CreditAccounts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestFingerprint = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ResourceType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                    ResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResponseStatus = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRequests", x => x.Id);
                    table.CheckConstraint("CK_IdempotencyRequests_CompletedState", "[Status] <> 'Completed' OR ([ResponseStatus] IS NOT NULL AND [CompletedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_IdempotencyRequests_ResponseStatus", "[ResponseStatus] IS NULL OR ([ResponseStatus] >= 200 AND [ResponseStatus] <= 599)");
                    table.ForeignKey(
                        name: "FK_IdempotencyRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IdentityTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Purpose = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdentityTokens", x => x.Id);
                    table.CheckConstraint("CK_IdentityTokens_ExpiryAfterCreated", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_IdentityTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeduplicationKey = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AvailableAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    LeaseOwner = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    DeadLetteredAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                    table.CheckConstraint("CK_OutboxMessages_AttemptNonNegative", "[AttemptCount] >= 0");
                    table.CheckConstraint("CK_OutboxMessages_JsonValid", "ISJSON([PayloadJson]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "RefreshSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", fixedLength: true, maxLength: 32, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: true),
                    ReplacedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshSessions", x => x.Id);
                    table.CheckConstraint("CK_RefreshSessions_ExpiryAfterCreated", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_RefreshSessions_RefreshSessions_ReplacedById",
                        column: x => x.ReplacedById,
                        principalTable: "RefreshSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefreshSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditLedger",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GenerationTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Bucket = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Delta = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    ReversesEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OperationKey = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                    UserId1 = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditLedger", x => x.Id);
                    table.CheckConstraint("CK_CreditLedger_DeltaNonZero", "[Delta] <> 0");
                    table.ForeignKey(
                        name: "FK_CreditLedger_CreditAccounts_UserId",
                        column: x => x.UserId,
                        principalTable: "CreditAccounts",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditLedger_CreditLedger_ReversesEntryId",
                        column: x => x.ReversesEntryId,
                        principalTable: "CreditLedger",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditLedger_GenerationTasks_GenerationTaskId",
                        column: x => x.GenerationTaskId,
                        principalTable: "GenerationTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditLedger_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditLedger_Users_UserId1",
                        column: x => x.UserId1,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "UX_Users_NormalizedEmail_Active",
                table: "Users",
                column: "NormalizedEmail",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Audit_AggregateTime",
                table: "AuditLogs",
                columns: new[] { "AggregateType", "AggregateId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_UserPurposeTime",
                table: "ConsentRecords",
                columns: new[] { "UserId", "Purpose", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedger_ActorUserId",
                table: "CreditLedger",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditLedger_UserId1",
                table: "CreditLedger",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_Ledger_UserTime",
                table: "CreditLedger",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Ledger_JobDebit",
                table: "CreditLedger",
                column: "GenerationTaskId",
                unique: true,
                filter: "[Kind] = 'GenerationDebit'");

            migrationBuilder.CreateIndex(
                name: "UX_Ledger_RefundOnce",
                table: "CreditLedger",
                column: "ReversesEntryId",
                unique: true,
                filter: "[ReversesEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Ledger_UserOperation",
                table: "CreditLedger",
                columns: new[] { "UserId", "OperationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_IdempotencyRequests_UserOpClient",
                table: "IdempotencyRequests",
                columns: new[] { "UserId", "Operation", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdentityTokens_UserId",
                table: "IdentityTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_IdentityTokens_TokenHash",
                table: "IdentityTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_Pending",
                table: "OutboxMessages",
                column: "AvailableAtUtc",
                filter: "[ProcessedAtUtc] IS NULL AND [DeadLetteredAtUtc] IS NULL")
                .Annotation("SqlServer:Include", new[] { "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_Outbox_DeduplicationKey",
                table: "OutboxMessages",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshSessions_ReplacedById",
                table: "RefreshSessions",
                column: "ReplacedById");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshSessions_UserFamily",
                table: "RefreshSessions",
                columns: new[] { "UserId", "FamilyId" });

            migrationBuilder.CreateIndex(
                name: "UX_RefreshSessions_TokenHash",
                table: "RefreshSessions",
                column: "TokenHash",
                unique: true);

            // Backfill existing users: NormalizedEmail, AccountStatus = Active, EmailVerifiedAtUtc
            migrationBuilder.Sql(@"
UPDATE [Users]
SET [NormalizedEmail] = UPPER(LTRIM(RTRIM([Email]))),
    [AccountStatus] = 'Active',
    [EmailVerifiedAtUtc] = SYSUTCDATETIME()
WHERE [NormalizedEmail] = '' OR [NormalizedEmail] IS NULL;
");

            // Backfill CreditAccounts for existing users
            migrationBuilder.Sql(@"
INSERT INTO [CreditAccounts] ([UserId], [TrialBalance], [PurchasedBalance])
SELECT u.[Id], u.[FreeTrialRemaining], u.[PurchasedRemaining]
FROM [Users] u
WHERE NOT EXISTS (SELECT 1 FROM [CreditAccounts] ca WHERE ca.[UserId] = u.[Id]);
");

            // Backfill CreditLedger opening balance for Trial credits
            migrationBuilder.Sql(@"
INSERT INTO [CreditLedger] ([Id], [UserId], [Bucket], [Delta], [Kind], [OperationKey], [Reason], [CreatedAtUtc])
SELECT NEWID(), u.[Id], 'Trial', u.[FreeTrialRemaining], 'OpeningBalance', 'migration:opening:trial:' + CONVERT(varchar(36), u.[Id]), 'Migration opening balance', SYSUTCDATETIME()
FROM [Users] u
WHERE u.[FreeTrialRemaining] > 0
  AND NOT EXISTS (SELECT 1 FROM [CreditLedger] cl WHERE cl.[UserId] = u.[Id] AND cl.[OperationKey] = 'migration:opening:trial:' + CONVERT(varchar(36), u.[Id]));
");

            // Backfill CreditLedger opening balance for Purchased credits
            migrationBuilder.Sql(@"
INSERT INTO [CreditLedger] ([Id], [UserId], [Bucket], [Delta], [Kind], [OperationKey], [Reason], [CreatedAtUtc])
SELECT NEWID(), u.[Id], 'Purchased', u.[PurchasedRemaining], 'OpeningBalance', 'migration:opening:purchased:' + CONVERT(varchar(36), u.[Id]), 'Migration opening balance', SYSUTCDATETIME()
FROM [Users] u
WHERE u.[PurchasedRemaining] > 0
  AND NOT EXISTS (SELECT 1 FROM [CreditLedger] cl WHERE cl.[UserId] = u.[Id] AND cl.[OperationKey] = 'migration:opening:purchased:' + CONVERT(varchar(36), u.[Id]));
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ConsentRecords");

            migrationBuilder.DropTable(
                name: "CreditLedger");

            migrationBuilder.DropTable(
                name: "IdempotencyRequests");

            migrationBuilder.DropTable(
                name: "IdentityTokens");

            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropTable(
                name: "RefreshSessions");

            migrationBuilder.DropTable(
                name: "CreditAccounts");

            migrationBuilder.DropIndex(
                name: "UX_Users_NormalizedEmail_Active",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AccountStatus",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailVerifiedAtUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FailedLoginAttempts",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockoutEndUtc",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Orders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "ThÃ nh phá»‘ Há»“ ChÃ­ Minh",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldDefaultValue: "ThÃƒÂ nh phÃ¡Â»â€˜ HÃ¡Â»â€œ ChÃƒÂ­ Minh");
        }
    }
}
