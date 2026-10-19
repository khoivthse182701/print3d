using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TheBoys.Domain.Entities;

namespace TheBoys.Infrastructure.Persistence.Configurations;

// ==============================================================================
// BẢNG 1: Users (Đã gộp UserCredits: Quota 2 lượt dùng thử)
// ==============================================================================
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", t =>
        {
            t.HasCheckConstraint("CK_Users_CreditsNonNegative", "[FreeTrialRemaining] >= 0 AND [PurchasedRemaining] >= 0 AND [TotalUsed] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.FullName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(20)
            .IsUnicode(false);

        builder.Property(x => x.DeviceFingerprint)
            .HasMaxLength(100)
            .IsUnicode(false);

        builder.Property(x => x.FreeTrialRemaining)
            .HasDefaultValue(2)
            .IsRequired();

        builder.Property(x => x.PurchasedRemaining)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.TotalUsed)
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasIndex(x => x.Email)
            .HasDatabaseName("UX_Users_Email_Active")
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(x => x.DeviceFingerprint)
            .HasDatabaseName("IX_Users_DeviceFingerprint")
            .HasFilter("[DeviceFingerprint] IS NOT NULL AND [IsDeleted] = 0");
    }
}

// ==============================================================================
// BẢNG 2: PricingPackages (Danh mục gói thành phẩm cá nhân hóa)
// ==============================================================================
public sealed class PricingPackageConfiguration : IEntityTypeConfiguration<PricingPackage>
{
    public void Configure(EntityTypeBuilder<PricingPackage> builder)
    {
        builder.ToTable("PricingPackages", t =>
        {
            t.HasCheckConstraint("CK_PricingPackages_Metrics", "[PriceVnd] > 0 AND [PartnerBaseCostVnd] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PackageCode)
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.PriceVnd)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.PartnerBaseCostVnd)
            .HasPrecision(18, 2)
            .HasDefaultValue(120000.00m)
            .IsRequired();

        builder.Property(x => x.Material)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.HeightRangeCm)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.HasIndex(x => x.PackageCode)
            .HasDatabaseName("UX_PricingPackages_Code")
            .IsUnique();
    }
}

// ==============================================================================
// BẢNG 3: PartnerLabs (Xưởng in 3D đối tác TP.HCM)
// ==============================================================================
public sealed class PartnerLabConfiguration : IEntityTypeConfiguration<PartnerLab>
{
    public void Configure(EntityTypeBuilder<PartnerLab> builder)
    {
        builder.ToTable("PartnerLabs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.LabName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ContactPerson).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(255);
        builder.Property(x => x.District).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(300).IsRequired();
        builder.Property(x => x.CapacityPerDay).HasDefaultValue(20);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

// ==============================================================================
// BẢNG 4: GenerationTasks (Đã gộp ModelAssets: Quản lý tác vụ & file 3D)
// ==============================================================================
public sealed class GenerationTaskConfiguration : IEntityTypeConfiguration<GenerationTask>
{
    public void Configure(EntityTypeBuilder<GenerationTask> builder)
    {
        builder.ToTable("GenerationTasks", t =>
        {
            t.HasCheckConstraint("CK_GenerationTasks_Metrics", "([Polycount] IS NULL OR [Polycount] >= 0) AND ([VertexCount] IS NULL OR [VertexCount] >= 0)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Prompt).HasMaxLength(1000);
        builder.Property(x => x.ArtStyle).HasMaxLength(50);
        builder.Property(x => x.ProviderTaskId).HasMaxLength(150).IsUnicode(false);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.InputImageUrl).HasMaxLength(1000);
        builder.Property(x => x.CleanedImageUrl).HasMaxLength(1000);

        builder.Property(x => x.ModelName).HasMaxLength(200);
        builder.Property(x => x.StoragePathGlb).HasMaxLength(1000);
        builder.Property(x => x.StoragePathStl).HasMaxLength(1000);
        builder.Property(x => x.ThumbnailUrl).HasMaxLength(1000);
        builder.Property(x => x.BoundingBoxX_Mm).HasPrecision(8, 2);
        builder.Property(x => x.BoundingBoxY_Mm).HasPrecision(8, 2);
        builder.Property(x => x.BoundingBoxZ_Mm).HasPrecision(8, 2);
        builder.Property(x => x.MinWallThicknessMm).HasPrecision(5, 2);
        builder.Property(x => x.EstimatedVolumeCm3).HasPrecision(8, 2);

        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasIndex(x => new { x.UserId, x.Status, x.CreatedAtUtc })
            .HasDatabaseName("IX_GenerationTasks_User_Status_ESR")
            .IncludeProperties(x => new { x.TaskType, x.ProviderTaskId });

        builder.HasIndex(x => new { x.UserId, x.IsApprovedForPrinting, x.CreatedAtUtc })
            .HasDatabaseName("IX_GenerationTasks_User_Library_ESR")
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(x => x.User)
            .WithMany(u => u.GenerationTasks)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// ==============================================================================
// BẢNG 5: Orders (Đã gộp Defect 5% & Mốc thời gian Timeline)
// ==============================================================================
public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", t =>
        {
            t.HasCheckConstraint("CK_Orders_Financials", "[SubTotalVnd] >= 0 AND [TotalAmount] >= 0 AND [Quantity] > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderCode)
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();

        // Strategic Financial Snapshots
        builder.Property(x => x.UnitPriceSnapshot).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.PartnerCostSnapshot).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.AiCostBudgetSnapshot).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.DefectBudgetSnapshot).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.ShippingSubsidySnapshot).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.PaymentGatewayFeeSnapshot).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.SubTotalVnd).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.ShippingFeeRaw).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.FinalShippingFee).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2).IsRequired();

        builder.Property(x => x.Quantity).HasDefaultValue(1);
        builder.Property(x => x.ReceiverName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.ReceiverPhone).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.StreetAddress).HasMaxLength(300).IsRequired();
        builder.Property(x => x.District).HasMaxLength(100).IsRequired();
        builder.Property(x => x.City).HasMaxLength(100).HasDefaultValue("Thành phố Hồ Chí Minh").IsRequired();
        builder.Property(x => x.CustomEngravingText).HasMaxLength(150);
        builder.Property(x => x.CustomerNote).HasMaxLength(500);
        builder.Property(x => x.TrackingNumber).HasMaxLength(100).IsUnicode(false);

        builder.Property(x => x.DefectReason).HasMaxLength(500);
        builder.Property(x => x.EvidencePhotoUrl).HasMaxLength(1000);
        builder.Property(x => x.EstimatedDefectCostVnd).HasPrecision(18, 2).HasDefaultValue(6000.00m);
        builder.Property(x => x.StatusNote).HasMaxLength(500);

        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.OrderCode).HasDatabaseName("UX_Orders_OrderCode").IsUnique();

        builder.HasIndex(x => new { x.UserId, x.Status, x.CreatedAtUtc })
            .HasDatabaseName("IX_Orders_User_Status_ESR")
            .IncludeProperties(x => new { x.OrderCode, x.TotalAmount });

        builder.HasIndex(x => new { x.PartnerLabId, x.Status, x.CreatedAtUtc })
            .HasDatabaseName("IX_Orders_Partner_Queue_ESR")
            .HasFilter("[PartnerLabId] IS NOT NULL");

        builder.HasOne(x => x.User)
            .WithMany(u => u.Orders)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(x => x.User)
            .IsRequired(false);

        builder.HasOne(x => x.GenerationTask)
            .WithMany()
            .HasForeignKey(x => x.GenerationTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        // Tell EF Core that while GenerationTaskId is required in DB, 
        // the navigation can be null in memory if GenerationTask was soft-deleted (resolves Warning 10622)
        builder.Navigation(x => x.GenerationTask)
            .IsRequired(false);

        builder.HasOne(x => x.PricingPackage)
            .WithMany()
            .HasForeignKey(x => x.PricingPackageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.PartnerLab)
            .WithMany(p => p.Orders)
            .HasForeignKey(x => x.PartnerLabId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// ==============================================================================
// BẢNG 6: Payments (Giao dịch thanh toán trực tuyến)
// ==============================================================================
public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", t =>
        {
            t.HasCheckConstraint("CK_Payments_PositiveAmount", "[Amount] > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PaymentMethod)
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.TransactionReference)
            .HasMaxLength(150)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.HasIndex(x => x.TransactionReference)
            .HasDatabaseName("UX_Payments_TransactionRef")
            .IsUnique();

        builder.HasIndex(x => new { x.OrderId, x.Status })
            .HasDatabaseName("IX_Payments_Order_Status");

        builder.HasOne(x => x.Order)
            .WithMany(o => o.Payments)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
