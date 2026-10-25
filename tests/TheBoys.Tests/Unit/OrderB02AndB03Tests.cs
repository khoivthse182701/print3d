using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TheBoys.Application.Approvals.Commands.CreateCustomerApproval;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Orders.Commands.CheckoutQuote;
using TheBoys.Application.Quotes;
using TheBoys.Application.Quotes.Commands.CreateQuote;
using TheBoys.Application.Quotes.Queries;
using TheBoys.Application.Reviews;
using TheBoys.Application.Reviews.Commands.CreatePrintReview;
using TheBoys.Domain.Entities;
using TheBoys.Infrastructure.Persistence;
using TheBoys.WebApi;

namespace TheBoys.Tests.Unit;

public class TestClock : IClock
{
    public DateTime UtcNow { get; set; } = DateTime.UtcNow;
}

public class OrderB02AndB03Tests(DemoFactory factory) : IClassFixture<DemoFactory>
{
    private (IServiceScope scope, ApplicationDbContext db, ModelProject project, ModelVersion version, PricingPackage package) CreateTestContext()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var project = new ModelProject
        {
            Id = Guid.NewGuid(),
            UserId = factory.CustomerId,
            Name = "Unit Test Model Project",
            CreatedAtUtc = DateTime.UtcNow
        };

        var version = new ModelVersion
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            VersionNo = 1,
            GeometryHash = SHA256.HashData(new byte[] { 1, 2, 3 }),
            WidthMm = 50,
            DepthMm = 50,
            HeightMm = 100,
            VolumeCm3 = 25,
            IsWatertight = true,
            PrintabilityStatus = "healthy",
            IsPublished = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var asset = new ModelAsset
        {
            Id = Guid.NewGuid(),
            VersionId = version.Id,
            Format = "STL",
            Purpose = "Print",
            StorageKey = $"models/unit_test_{Guid.NewGuid():N}.stl",
            Sha256 = SHA256.HashData(new byte[] { 4, 5, 6 }),
            ByteLength = 1024,
            CreatedAtUtc = DateTime.UtcNow
        };
        version.Assets.Add(asset);

        db.ModelProjects.Add(project);
        db.ModelVersions.Add(version);
        db.SaveChanges();

        var package = db.PricingPackages.Single(p => p.Id == DatabaseInitializer.DemoPackageId);

        return (scope, db, project, version, package);
    }

    [Fact]
    public async Task Invariant1_PrintReview_AppendOnly_RejectedThenApproved_CreatesTwoHistories()
    {
        var (scope, db, _, version, _) = CreateTestContext();
        using var _ = scope;
        var hasher = scope.ServiceProvider.GetRequiredService<IConfigurationHasher>();
        var clock = new TestClock();
        var handler = new CreatePrintReviewCommandHandler(db, hasher, clock);

        var config = new { material = "Resin 16K", heightMm = 100 };
        var checklist = new PrintReviewChecklistDto
        {
            Orientation = "45 degrees upright",
            SupportConfirmation = true,
            Material = "Resin 16K",
            PrintHeightMm = 100,
            BuildVolumeConfirmation = true
        };

        // 1. Review Rejected
        var rejectCmd = new CreatePrintReviewCommand(
            version.Id,
            factory.StaffId,
            "Rejected",
            checklist,
            config,
            null,
            "Độ dày thành chưa đạt yêu cầu"
        );
        var rejectRes = await handler.Handle(rejectCmd, CancellationToken.None);
        Assert.True(rejectRes.IsSuccess);
        Assert.Equal("Rejected", rejectRes.Value.Decision);

        // 2. Review Approved
        var approveCmd = new CreatePrintReviewCommand(
            version.Id,
            factory.StaffId,
            "Approved",
            checklist,
            config,
            "evidence/photo.png",
            "Đã kiểm tra file và bù co ngót thành công"
        );
        var approveRes = await handler.Handle(approveCmd, CancellationToken.None);
        Assert.True(approveRes.IsSuccess);
        Assert.Equal("Approved", approveRes.Value.Decision);

        // Verify append-only: 2 separate records exist in history
        var reviews = await db.PrintReviews.Where(r => r.VersionId == version.Id).ToListAsync();
        Assert.Equal(2, reviews.Count);
        Assert.Contains(reviews, r => r.Decision == "Rejected" && r.Note.Contains("Độ dày"));
        Assert.Contains(reviews, r => r.Decision == "Approved" && r.Note.Contains("bù co ngót"));
    }

    [Fact]
    public async Task Invariant2_PrintReview_CannotApprove_WhenInvalid()
    {
        var (scope, db, _, version, _) = CreateTestContext();
        using var _ = scope;
        var hasher = scope.ServiceProvider.GetRequiredService<IConfigurationHasher>();
        var clock = new TestClock();
        var handler = new CreatePrintReviewCommandHandler(db, hasher, clock);

        var config = new { material = "Resin 16K", heightMm = 100 };

        // Test non-watertight version
        version.IsWatertight = false;
        await db.SaveChangesAsync();

        var checklist = new PrintReviewChecklistDto
        {
            Orientation = "upright",
            SupportConfirmation = true,
            Material = "Resin 16K",
            PrintHeightMm = 100,
            BuildVolumeConfirmation = true
        };

        var cmd = new CreatePrintReviewCommand(version.Id, factory.StaffId, "Approved", checklist, config, null, "Approved note");
        var res = await handler.Handle(cmd, CancellationToken.None);
        Assert.True(res.IsFailure);
        Assert.Equal("MeshCheckFailed", res.Error.Code);

        // Fix watertight, test invalid checklist (missing support confirmation without exception)
        version.IsWatertight = true;
        await db.SaveChangesAsync();

        checklist.SupportConfirmation = false;
        checklist.ExceptionReason = null;
        var res2 = await handler.Handle(cmd, CancellationToken.None);
        Assert.True(res2.IsFailure);
        Assert.Equal("SupportRequired", res2.Error.Code);
    }

    [Fact]
    public async Task Invariant3_CustomerApproval_OnlyOwnerCanApprove()
    {
        var (scope, db, _, version, _) = CreateTestContext();
        using var _ = scope;
        var hasher = scope.ServiceProvider.GetRequiredService<IConfigurationHasher>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
        var clock = new TestClock();
        var handler = new CreateCustomerApprovalCommandHandler(db, hasher, audit, clock);

        var config = new { material = "Resin 16K", heightMm = 100 };

        // Other customer attempts approval
        var strangerCmd = new CreateCustomerApprovalCommand(factory.OtherId, version.Id, config);
        var strangerRes = await handler.Handle(strangerCmd, CancellationToken.None);
        Assert.True(strangerRes.IsFailure);
        Assert.Equal("NotOwner", strangerRes.Error.Code);

        // Real owner approves
        var ownerCmd = new CreateCustomerApprovalCommand(factory.CustomerId, version.Id, config);
        var ownerRes = await handler.Handle(ownerCmd, CancellationToken.None);
        Assert.True(ownerRes.IsSuccess);
        Assert.Equal(factory.CustomerId, ownerRes.Value.UserId);
    }

    [Fact]
    public void Invariant4_ConfigurationHash_Divergence()
    {
        using var scope = factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IConfigurationHasher>();

        // Object with different property declaration order
        var objA = new { material = "Resin 16K", heightMm = 100m, packageCode = "TIER_STANDARD_390" };
        var objB = new { packageCode = "TIER_STANDARD_390", heightMm = 100m, material = "Resin 16K" };

        var (hashA, canonicalA) = hasher.ComputeHash(objA);
        var (hashB, canonicalB) = hasher.ComputeHash(objB);

        Assert.Equal(canonicalA, canonicalB);
        Assert.Equal(Convert.ToHexString(hashA), Convert.ToHexString(hashB));

        // Object with changed production parameter
        var objC = new { material = "PLA", heightMm = 100m, packageCode = "TIER_STANDARD_390" };
        var (hashC, _) = hasher.ComputeHash(objC);

        Assert.NotEqual(Convert.ToHexString(hashA), Convert.ToHexString(hashC));
    }

    [Fact]
    public async Task Invariant5_Quote_SavesSnapshot_And_ExpiresProperly()
    {
        var (scope, db, _, version, package) = CreateTestContext();
        using var _ = scope;
        var hasher = scope.ServiceProvider.GetRequiredService<IConfigurationHasher>();
        var clock = new TestClock { UtcNow = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc) };
        var quoteOptions = Options.Create(new QuoteOptions { TtlMinutes = 30, DefaultShippingFeeVnd = 15000 });

        var config = new { material = "Resin 16K", heightMm = 100m };
        var (hash, _) = hasher.ComputeHash(config);

        // Create approved review
        var review = PrintReview.Create(version.Id, hash, factory.StaffId, "Approved", "{}", null, "OK", clock.UtcNow);
        db.PrintReviews.Add(review);

        // Create customer approval
        var approval = CustomerApproval.Create(factory.CustomerId, version.Id, hash, "{}", "v1.0", clock.UtcNow);
        db.CustomerApprovals.Add(approval);
        await db.SaveChangesAsync();

        var quoteHandler = new CreateQuoteCommandHandler(db, hasher, quoteOptions, clock);
        var quoteCmd = new CreateQuoteCommand(
            factory.CustomerId,
            version.Id,
            config,
            package.Id,
            Quantity: 2,
            new ShippingAddressDto
            {
                ReceiverName = "Nguyen Van A",
                ReceiverPhone = "0901234567",
                StreetAddress = "123 Le Loi",
                District = "District 1",
                City = "TP HCM"
            }
        );

        var quoteRes = await quoteHandler.Handle(quoteCmd, CancellationToken.None);
        Assert.True(quoteRes.IsSuccess);
        var quoteDto = quoteRes.Value;

        // Verify financial calculations
        Assert.Equal(240625, quoteDto.UnitPriceVnd);
        Assert.Equal(15000, quoteDto.ShippingFeeVnd);
        Assert.Equal(240625 * 2 + 15000, quoteDto.TotalVnd);
        Assert.Equal(clock.UtcNow.AddMinutes(30), quoteDto.ExpiresAtUtc);
        Assert.False(quoteDto.IsExpired);

        // Verify expiry after 31 minutes
        clock.UtcNow = clock.UtcNow.AddMinutes(31);
        var getHandler = new GetQuoteByIdQueryHandler(db, clock);
        var getRes = await getHandler.Handle(new GetQuoteByIdQuery(quoteDto.Id, factory.CustomerId, false), CancellationToken.None);
        Assert.True(getRes.IsSuccess);
        Assert.True(getRes.Value.IsExpired);
    }

    [Fact]
    public async Task Invariant6_Quote_PackagePriceChange_DoesNotAffectExistingQuote()
    {
        var (scope, db, _, version, package) = CreateTestContext();
        using var _ = scope;
        var hasher = scope.ServiceProvider.GetRequiredService<IConfigurationHasher>();
        var clock = new TestClock();
        var quoteOptions = Options.Create(new QuoteOptions { TtlMinutes = 30, DefaultShippingFeeVnd = 0 });

        var config = new { material = "Resin 16K", heightMm = 100m };
        var (hash, _) = hasher.ComputeHash(config);

        db.PrintReviews.Add(PrintReview.Create(version.Id, hash, factory.StaffId, "Approved", "{}", null, "OK", clock.UtcNow));
        db.CustomerApprovals.Add(CustomerApproval.Create(factory.CustomerId, version.Id, hash, "{}", "v1.0", clock.UtcNow));
        await db.SaveChangesAsync();

        var quoteHandler = new CreateQuoteCommandHandler(db, hasher, quoteOptions, clock);
        var quoteRes = await quoteHandler.Handle(new CreateQuoteCommand(
            factory.CustomerId, version.Id, config, package.Id, 1,
            new ShippingAddressDto { ReceiverName = "A", ReceiverPhone = "090", StreetAddress = "S", District = "D", City = "C" }
        ), CancellationToken.None);

        Assert.True(quoteRes.IsSuccess);
        var quoteId = quoteRes.Value.Id;
        Assert.Equal(240625, quoteRes.Value.TotalVnd);

        // Mutate package price in memory/db
        var originalPrice = package.PriceVnd;
        package.PriceVnd = 600000;
        await db.SaveChangesAsync();

        // Re-read quote: price must still be 390.000 VND (immutable historical snapshot)
        var quoteInDb = await db.Quotes.SingleAsync(q => q.Id == quoteId);
        Assert.Equal(240625, quoteInDb.UnitPriceVnd);
        Assert.Equal(240625, quoteInDb.TotalVnd);

        // Reset package price back
        package.PriceVnd = originalPrice;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Invariant7_Quote_CannotCreate_WithRejectedReview_Or_InactivePackage()
    {
        var (scope, db, _, version, package) = CreateTestContext();
        using var _ = scope;
        var hasher = scope.ServiceProvider.GetRequiredService<IConfigurationHasher>();
        var clock = new TestClock();
        var quoteOptions = Options.Create(new QuoteOptions { TtlMinutes = 30 });

        var config = new { material = "Resin 16K", heightMm = 100m };
        var (hash, _) = hasher.ComputeHash(config);

        // Only Rejected review exists
        db.PrintReviews.Add(PrintReview.Create(version.Id, hash, factory.StaffId, "Rejected", "{}", null, "Too thin", clock.UtcNow));
        db.CustomerApprovals.Add(CustomerApproval.Create(factory.CustomerId, version.Id, hash, "{}", "v1.0", clock.UtcNow));
        await db.SaveChangesAsync();

        var quoteHandler = new CreateQuoteCommandHandler(db, hasher, quoteOptions, clock);
        var resRejected = await quoteHandler.Handle(new CreateQuoteCommand(
            factory.CustomerId, version.Id, config, package.Id, 1,
            new ShippingAddressDto { ReceiverName = "A", ReceiverPhone = "090", StreetAddress = "S", District = "D", City = "C" }
        ), CancellationToken.None);

        Assert.True(resRejected.IsFailure);
        Assert.Equal("ReviewRequired", resRejected.Error.Code);

        // Add Approved review, but deactivate package
        db.PrintReviews.Add(PrintReview.Create(version.Id, hash, factory.StaffId, "Approved", "{}", null, "Fixed", clock.UtcNow));
        package.IsActive = false;
        await db.SaveChangesAsync();

        var resInactive = await quoteHandler.Handle(new CreateQuoteCommand(
            factory.CustomerId, version.Id, config, package.Id, 1,
            new ShippingAddressDto { ReceiverName = "A", ReceiverPhone = "090", StreetAddress = "S", District = "D", City = "C" }
        ), CancellationToken.None);

        Assert.True(resInactive.IsFailure);
        Assert.Equal("PackageNotFound", resInactive.Error.Code);

        // Restore package active
        package.IsActive = true;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Invariant8to12_Checkout_Atomicity_Idempotency_And_Expiry()
    {
        var (scope, db, _, version, package) = CreateTestContext();
        using var _ = scope;
        var hasher = scope.ServiceProvider.GetRequiredService<IConfigurationHasher>();
        var clock = new TestClock { UtcNow = new DateTime(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc) };
        var quoteOptions = Options.Create(new QuoteOptions { TtlMinutes = 30, DefaultShippingFeeVnd = 10000 });

        var config = new { material = "Resin 16K", heightMm = 100m };
        var (hash, _) = hasher.ComputeHash(config);

        var review = PrintReview.Create(version.Id, hash, factory.StaffId, "Approved", "{}", null, "OK", clock.UtcNow);
        var approval = CustomerApproval.Create(factory.CustomerId, version.Id, hash, "{}", "v1.0", clock.UtcNow);
        db.PrintReviews.Add(review);
        db.CustomerApprovals.Add(approval);
        await db.SaveChangesAsync();

        var quoteHandler = new CreateQuoteCommandHandler(db, hasher, quoteOptions, clock);
        var quoteRes = await quoteHandler.Handle(new CreateQuoteCommand(
            factory.CustomerId, version.Id, config, package.Id, 2,
            new ShippingAddressDto { ReceiverName = "Customer 1", ReceiverPhone = "090", StreetAddress = "Street 1", District = "District 1", City = "HCM" }
        ), CancellationToken.None);

        var quoteId = quoteRes.Value.Id;

        // Invariant 8: Successful checkout creates Order + OrderItem + OrderStatusHistory atomically
        var checkoutHandler = new CheckoutQuoteCommandHandler(db, clock);
        var clientKey1 = Guid.NewGuid().ToString("N");
        var fingerprint1 = "fingerprint_1";

        var checkoutRes1 = await checkoutHandler.Handle(new CheckoutQuoteCommand(factory.CustomerId, quoteId, clientKey1, fingerprint1), CancellationToken.None);
        Assert.True(checkoutRes1.IsSuccess);
        Assert.False(checkoutRes1.Value.IsReplay);

        var order = checkoutRes1.Value.Order;
        Assert.Equal("PendingPayment", order.FulfillmentStatus);
        Assert.Equal("Unpaid", order.OrderPaymentStatus);
        Assert.Equal(quoteId, order.QuoteId);
        Assert.Equal(240625 * 2 + 10000, order.TotalAmount);

        // Verify OrderItem and OrderStatusHistory in DB
        var savedOrder = await db.Orders
            .Include(o => o.Items)
            .Include(o => o.StatusHistories)
            .SingleAsync(o => o.Id == order.Id);

        Assert.Single(savedOrder.Items);
        Assert.Equal(1, savedOrder.Items.First().LineNumber);
        Assert.Equal(2, savedOrder.Items.First().Quantity);
        Assert.Equal(240625, savedOrder.Items.First().UnitPriceVnd);

        Assert.Equal(2, savedOrder.StatusHistories.Count);
        Assert.Contains(savedOrder.StatusHistories, h => h.Axis == "Fulfillment" && h.ToStatus == "PendingPayment");
        Assert.Contains(savedOrder.StatusHistories, h => h.Axis == "Payment" && h.ToStatus == "Unpaid");

        // Invariant 9: Replay with same idempotency key
        var replayRes = await checkoutHandler.Handle(new CheckoutQuoteCommand(factory.CustomerId, quoteId, clientKey1, fingerprint1), CancellationToken.None);
        Assert.True(replayRes.IsSuccess);
        Assert.True(replayRes.Value.IsReplay);
        Assert.Equal(order.Id, replayRes.Value.Order.Id);

        // Invariant 10: Same idempotency key with different fingerprint -> Conflict
        var mismatchRes = await checkoutHandler.Handle(new CheckoutQuoteCommand(factory.CustomerId, quoteId, clientKey1, "different_fingerprint"), CancellationToken.None);
        Assert.True(mismatchRes.IsFailure);
        Assert.Equal("IdempotencyMismatch", mismatchRes.Error.Code);

        // Invariant 12: Cannot checkout already used quote with a new key
        var clientKey2 = Guid.NewGuid().ToString("N");
        var secondCheckoutRes = await checkoutHandler.Handle(new CheckoutQuoteCommand(factory.CustomerId, quoteId, clientKey2, "fingerprint_2"), CancellationToken.None);
        Assert.True(secondCheckoutRes.IsFailure);
        Assert.Equal("QuoteAlreadyCheckedOut", secondCheckoutRes.Error.Code);

        // Invariant 12: Cannot checkout expired quote
        var expiredQuoteRes = await quoteHandler.Handle(new CreateQuoteCommand(
            factory.CustomerId, version.Id, config, package.Id, 1,
            new ShippingAddressDto { ReceiverName = "A", ReceiverPhone = "090", StreetAddress = "S", District = "D", City = "C" }
        ), CancellationToken.None);
        var expiredQuoteId = expiredQuoteRes.Value.Id;

        clock.UtcNow = clock.UtcNow.AddMinutes(31);
        var expiredCheckoutRes = await checkoutHandler.Handle(new CheckoutQuoteCommand(factory.CustomerId, expiredQuoteId, Guid.NewGuid().ToString("N"), "fp"), CancellationToken.None);
        Assert.True(expiredCheckoutRes.IsFailure);
        Assert.Equal("QuoteExpired", expiredCheckoutRes.Error.Code);
    }
}

