using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TheBoys.Infrastructure.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Both branches published an initial migration. Preserve both history IDs,
            // but create the shared tables only when the other baseline has not run.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory]
                    WHERE [MigrationId] = N'20261019080618_InitialSchema')
                BEGIN
                CREATE TABLE [PricingPackages] (
                    [Id] uniqueidentifier NOT NULL,
                    [PackageCode] varchar(50) NOT NULL,
                    [Name] nvarchar(150) NOT NULL,
                    [PriceVnd] decimal(18,2) NOT NULL,
                    [PartnerBaseCostVnd] decimal(18,2) NOT NULL DEFAULT 120000.0,
                    [Material] nvarchar(50) NOT NULL,
                    [HeightRangeCm] nvarchar(50) NOT NULL,
                    [Description] nvarchar(500) NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedAtUtc] datetime2(3) NOT NULL,
                    CONSTRAINT [PK_PricingPackages] PRIMARY KEY ([Id]),
                    CONSTRAINT [CK_PricingPackages_Metrics] CHECK ([PriceVnd] > 0 AND [PartnerBaseCostVnd] >= 0)
                );

                CREATE TABLE [Users] (
                    [Id] uniqueidentifier NOT NULL,
                    [Email] nvarchar(255) NOT NULL,
                    [PasswordHash] nvarchar(max) NULL,
                    [FullName] nvarchar(150) NOT NULL,
                    [PhoneNumber] varchar(20) NULL,
                    [Role] int NOT NULL,
                    [DeviceFingerprint] varchar(100) NULL,
                    [IsPhoneVerified] bit NOT NULL,
                    [FreeTrialRemaining] int NOT NULL DEFAULT 2,
                    [PurchasedRemaining] int NOT NULL DEFAULT 0,
                    [TotalUsed] int NOT NULL DEFAULT 0,
                    [CreatedAtUtc] datetime2(3) NOT NULL,
                    [UpdatedAtUtc] datetime2(3) NULL,
                    [IsDeleted] bit NOT NULL,
                    [DeletedAtUtc] datetime2(3) NULL,
                    [RowVersion] rowversion NOT NULL,
                    CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
                    CONSTRAINT [CK_Users_CreditsNonNegative] CHECK ([FreeTrialRemaining] >= 0 AND [PurchasedRemaining] >= 0 AND [TotalUsed] >= 0)
                );

                CREATE TABLE [GenerationTasks] (
                    [Id] uniqueidentifier NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [TaskType] int NOT NULL,
                    [Prompt] nvarchar(1000) NULL,
                    [ArtStyle] nvarchar(50) NULL,
                    [InputImageUrl] nvarchar(1000) NULL,
                    [CleanedImageUrl] nvarchar(1000) NULL,
                    [Status] int NOT NULL,
                    [ProviderTaskId] varchar(150) NULL,
                    [ErrorMessage] nvarchar(1000) NULL,
                    [RetryCount] int NOT NULL,
                    [DurationSeconds] int NULL,
                    [CompletedAtUtc] datetime2(3) NULL,
                    [ModelName] nvarchar(200) NULL,
                    [StoragePathGlb] nvarchar(1000) NULL,
                    [StoragePathStl] nvarchar(1000) NULL,
                    [ThumbnailUrl] nvarchar(1000) NULL,
                    [Polycount] int NULL,
                    [VertexCount] int NULL,
                    [IsWatertight] bit NOT NULL,
                    [MinWallThicknessMm] decimal(5,2) NULL,
                    [BoundingBoxX_Mm] decimal(8,2) NULL,
                    [BoundingBoxY_Mm] decimal(8,2) NULL,
                    [BoundingBoxZ_Mm] decimal(8,2) NULL,
                    [EstimatedVolumeCm3] decimal(8,2) NULL,
                    [IsApprovedForPrinting] bit NOT NULL,
                    [CreatedAtUtc] datetime2(3) NOT NULL,
                    [UpdatedAtUtc] datetime2(3) NULL,
                    [IsDeleted] bit NOT NULL,
                    [DeletedAtUtc] datetime2(3) NULL,
                    [RowVersion] rowversion NOT NULL,
                    CONSTRAINT [PK_GenerationTasks] PRIMARY KEY ([Id]),
                    CONSTRAINT [CK_GenerationTasks_Metrics] CHECK (([Polycount] IS NULL OR [Polycount] >= 0) AND ([VertexCount] IS NULL OR [VertexCount] >= 0)),
                    CONSTRAINT [FK_GenerationTasks_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
                );

                CREATE TABLE [PartnerLabs] (
                    [Id] uniqueidentifier NOT NULL,
                    [UserId] uniqueidentifier NULL,
                    [LabName] nvarchar(200) NOT NULL,
                    [ContactPerson] nvarchar(100) NOT NULL,
                    [PhoneNumber] varchar(20) NOT NULL,
                    [Email] nvarchar(255) NULL,
                    [District] nvarchar(100) NOT NULL,
                    [Address] nvarchar(300) NOT NULL,
                    [CapacityPerDay] int NOT NULL DEFAULT 20,
                    [IsActive] bit NOT NULL,
                    [CreatedAtUtc] datetime2(3) NOT NULL,
                    CONSTRAINT [PK_PartnerLabs] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_PartnerLabs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
                );

                CREATE TABLE [Orders] (
                    [Id] uniqueidentifier NOT NULL,
                    [OrderCode] varchar(50) NOT NULL,
                    [UserId] uniqueidentifier NOT NULL,
                    [GenerationTaskId] uniqueidentifier NOT NULL,
                    [PricingPackageId] uniqueidentifier NOT NULL,
                    [PartnerLabId] uniqueidentifier NULL,
                    [Status] int NOT NULL,
                    [UnitPriceSnapshot] decimal(18,2) NOT NULL,
                    [PartnerCostSnapshot] decimal(18,2) NOT NULL,
                    [AiCostBudgetSnapshot] decimal(18,2) NOT NULL,
                    [DefectBudgetSnapshot] decimal(18,2) NOT NULL,
                    [ShippingSubsidySnapshot] decimal(18,2) NOT NULL,
                    [PaymentGatewayFeeSnapshot] decimal(18,2) NOT NULL,
                    [Quantity] int NOT NULL DEFAULT 1,
                    [SubTotalVnd] decimal(18,2) NOT NULL,
                    [ShippingFeeRaw] decimal(18,2) NOT NULL,
                    [FinalShippingFee] decimal(18,2) NOT NULL,
                    [TotalAmount] decimal(18,2) NOT NULL,
                    [CustomEngravingText] nvarchar(150) NULL,
                    [ReceiverName] nvarchar(150) NOT NULL,
                    [ReceiverPhone] varchar(20) NOT NULL,
                    [StreetAddress] nvarchar(300) NOT NULL,
                    [District] nvarchar(100) NOT NULL,
                    [City] nvarchar(100) NOT NULL DEFAULT N'Thành phố Hồ Chí Minh',
                    [CustomerNote] nvarchar(500) NULL,
                    [TrackingNumber] varchar(100) NULL,
                    [DefectReason] nvarchar(500) NULL,
                    [EvidencePhotoUrl] nvarchar(1000) NULL,
                    [EstimatedDefectCostVnd] decimal(18,2) NULL DEFAULT 6000.0,
                    [ReprintApproved] bit NOT NULL,
                    [CreatedAtUtc] datetime2(3) NOT NULL,
                    [PaidAtUtc] datetime2(3) NULL,
                    [SentToPartnerAtUtc] datetime2(3) NULL,
                    [PrintingStartedAtUtc] datetime2(3) NULL,
                    [PackagedAtUtc] datetime2(3) NULL,
                    [ShippedAtUtc] datetime2(3) NULL,
                    [DeliveredAtUtc] datetime2(3) NULL,
                    [CancelledAtUtc] datetime2(3) NULL,
                    [StatusNote] nvarchar(500) NULL,
                    [RowVersion] rowversion NOT NULL,
                    CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
                    CONSTRAINT [CK_Orders_Financials] CHECK ([SubTotalVnd] >= 0 AND [TotalAmount] >= 0 AND [Quantity] > 0),
                    CONSTRAINT [FK_Orders_GenerationTasks_GenerationTaskId] FOREIGN KEY ([GenerationTaskId]) REFERENCES [GenerationTasks] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_Orders_PartnerLabs_PartnerLabId] FOREIGN KEY ([PartnerLabId]) REFERENCES [PartnerLabs] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_Orders_PricingPackages_PricingPackageId] FOREIGN KEY ([PricingPackageId]) REFERENCES [PricingPackages] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_Orders_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
                );

                CREATE TABLE [Payments] (
                    [Id] uniqueidentifier NOT NULL,
                    [OrderId] uniqueidentifier NOT NULL,
                    [PaymentMethod] varchar(50) NOT NULL,
                    [TransactionReference] varchar(150) NOT NULL,
                    [Amount] decimal(18,2) NOT NULL,
                    [Status] int NOT NULL,
                    [GatewayResponseJson] nvarchar(max) NULL,
                    [CreatedAtUtc] datetime2(3) NOT NULL,
                    [PaidAtUtc] datetime2(3) NULL,
                    CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
                    CONSTRAINT [CK_Payments_PositiveAmount] CHECK ([Amount] > 0),
                    CONSTRAINT [FK_Payments_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION
                );

                CREATE INDEX [IX_GenerationTasks_User_Library_ESR] ON [GenerationTasks] ([UserId], [IsApprovedForPrinting], [CreatedAtUtc]) WHERE [IsDeleted] = 0;

                CREATE INDEX [IX_GenerationTasks_User_Status_ESR] ON [GenerationTasks] ([UserId], [Status], [CreatedAtUtc]) INCLUDE ([TaskType], [ProviderTaskId]);

                CREATE INDEX [IX_Orders_GenerationTaskId] ON [Orders] ([GenerationTaskId]);

                CREATE INDEX [IX_Orders_Partner_Queue_ESR] ON [Orders] ([PartnerLabId], [Status], [CreatedAtUtc]) WHERE [PartnerLabId] IS NOT NULL;

                CREATE INDEX [IX_Orders_PricingPackageId] ON [Orders] ([PricingPackageId]);

                CREATE INDEX [IX_Orders_User_Status_ESR] ON [Orders] ([UserId], [Status], [CreatedAtUtc]) INCLUDE ([OrderCode], [TotalAmount]);

                CREATE UNIQUE INDEX [UX_Orders_OrderCode] ON [Orders] ([OrderCode]);

                CREATE INDEX [IX_PartnerLabs_UserId] ON [PartnerLabs] ([UserId]);

                CREATE INDEX [IX_Payments_Order_Status] ON [Payments] ([OrderId], [Status]);

                CREATE UNIQUE INDEX [UX_Payments_TransactionRef] ON [Payments] ([TransactionReference]);

                CREATE UNIQUE INDEX [UX_PricingPackages_Code] ON [PricingPackages] ([PackageCode]);

                CREATE INDEX [IX_Users_DeviceFingerprint] ON [Users] ([DeviceFingerprint]) WHERE [DeviceFingerprint] IS NOT NULL AND [IsDeleted] = 0;

                CREATE UNIQUE INDEX [UX_Users_Email_Active] ON [Users] ([Email]) WHERE [IsDeleted] = 0;
                END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The last remaining baseline owns the shared tables during rollback.
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory]
                    WHERE [MigrationId] = N'20261019080618_InitialSchema')
                BEGIN
                    DROP TABLE [Payments];
                    DROP TABLE [Orders];
                    DROP TABLE [GenerationTasks];
                    DROP TABLE [PartnerLabs];
                    DROP TABLE [PricingPackages];
                    DROP TABLE [Users];
                END;
                """);
        }
    }
}