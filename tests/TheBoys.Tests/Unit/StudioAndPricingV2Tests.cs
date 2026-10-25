using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SkiaSharp;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Application.Common.Models;
using TheBoys.Application.Common.Payments;
using TheBoys.Application.Payments.Commands.ProcessPaymentWebhook;
using TheBoys.Application.Quotes.Commands.CreateQuote;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Payments;
using TheBoys.Infrastructure.Persistence;
using TheBoys.Infrastructure.Services;
using TheBoys.WebApi;

namespace TheBoys.Tests.Unit;

public class StudioAndPricingV2Tests(DemoFactory factory) : IClassFixture<DemoFactory>
{
    [Theory]
    [InlineData("anime girl with nude body", false)]
    [InlineData("mô hình khỏa thân nghệ thuật", false)]
    [InlineData("bạo lực chém giết dã man", false)]
    [InlineData("hentai figure 3d model", false)]
    [InlineData("vũ khí súng đạn hạng nặng", false)]
    [InlineData("chú chó corgi dễ thương đeo kính", true)]
    [InlineData("chiếc xe hơi cổ điển màu xanh", true)]
    [InlineData("Iron Man Mark 85 superhero sculpture", true)]
    public void ContentModerationService_Scans_Text_Bilingual_Accurate(string prompt, bool expectedSafe)
    {
        var service = new ContentModerationService();
        var isSafe = service.IsTextSafe(prompt);
        Assert.Equal(expectedSafe, isSafe);
    }

    [Fact]
    public void ContentModerationService_Blocks_High_Skin_Cluster_Image()
    {
        var service = new ContentModerationService();

        // 1. Create a 64x64 skin tone image (R: 220, G: 160, B: 130) -> skin cluster ratio 100% > 45%
        using var skinBitmap = new SKBitmap(64, 64);
        using (var canvas = new SKCanvas(skinBitmap))
        {
            canvas.Clear(new SKColor(220, 160, 130));
        }
        using var skinImage = SKImage.FromBitmap(skinBitmap);
        using var skinData = skinImage.Encode(SKEncodedImageFormat.Png, 100);
        var skinBytes = skinData.ToArray();

        var isSkinSafe = service.IsImageSafe(skinBytes);
        Assert.False(isSkinSafe);

        // 2. Create a 64x64 non-skin image (deep basalt green R: 40, G: 80, B: 40)
        using var greenBitmap = new SKBitmap(64, 64);
        using (var canvas = new SKCanvas(greenBitmap))
        {
            canvas.Clear(new SKColor(40, 80, 40));
        }
        using var greenImage = SKImage.FromBitmap(greenBitmap);
        using var greenData = greenImage.Encode(SKEncodedImageFormat.Png, 100);
        var greenBytes = greenData.ToArray();

        var isGreenSafe = service.IsImageSafe(greenBytes);
        Assert.True(isGreenSafe);
    }

    [Fact]
    public async Task AddPurchasedCreditsAsync_GrantsCredits_Idempotently()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var creditService = new CreditService(db, clock);

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = $"topup-{userId:N}@test.vn",
            NormalizedEmail = $"TOPUP-{userId:N}@TEST.VN",
            FullName = "Topup Customer",
            Role = UserRole.Customer,
            AccountStatus = AccountStatus.Active
        };
        var account = CreditAccount.Create(userId, trialBalance: 1, purchasedBalance: 0);
        db.Users.Add(user);
        db.CreditAccounts.Add(account);
        await db.SaveChangesAsync();

        var opKey = $"topup:test:{Guid.NewGuid():N}";

        // Grant 10 credits (Gói Sáng Tạo)
        var res1 = await creditService.AddPurchasedCreditsAsync(userId, 10, opKey, "Topup Gói Sáng Tạo", CancellationToken.None);
        Assert.True(res1.IsSuccess);
        Assert.Equal(10, res1.Value.RemainingPurchased);
        Assert.Equal(1, res1.Value.RemainingTrial);

        // Replay same operation key
        var resReplay = await creditService.AddPurchasedCreditsAsync(userId, 10, opKey, "Topup Gói Sáng Tạo", CancellationToken.None);
        Assert.True(resReplay.IsSuccess);
        Assert.Equal(10, resReplay.Value.RemainingPurchased);

        // Verify balance in DB
        var freshAccount = await db.CreditAccounts.SingleAsync(a => a.UserId == userId);
        Assert.Equal(10, freshAccount.PurchasedBalance);
        Assert.Equal(1, freshAccount.TrialBalance);
        Assert.Equal(11, freshAccount.TotalBalance);
    }

    [Fact]
    public async Task ProcessPaymentWebhook_TopUp_Grants_Credits_Correctly()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var reg = scope.ServiceProvider.GetRequiredService<IPaymentGatewayRegistry>();
        var creditService = new CreditService(db, clock);

        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = $"webhook-topup-{userId:N}@test.vn",
            NormalizedEmail = $"WEBHOOK-TOPUP-{userId:N}@TEST.VN",
            FullName = "Webhook Topup Customer",
            Role = UserRole.Customer,
            AccountStatus = AccountStatus.Active
        };
        var account = CreditAccount.Create(userId, trialBalance: 1, purchasedBalance: 0);
        db.Users.Add(user);
        db.CreditAccounts.Add(account);
        await db.SaveChangesAsync();

        var webhookHandler = new ProcessPaymentWebhookCommandHandler(db, reg, clock, creditService);

        var eventId = Guid.NewGuid().ToString("N");
        var topupReference = $"TOPUP_creator_{userId}_{Guid.NewGuid():N}";
        var payloadObj = new
        {
            code = "00",
            desc = "success",
            data = new
            {
                orderCode = 1000888999L,
                amount = 120000,
                description = topupReference.Length > 25 ? topupReference[..25] : topupReference,
                accountNumber = "123456789",
                reference = $"TX-{eventId}",
                transactionDateTime = clock.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                currency = "VND",
                paymentLinkId = "LINK-001",
                code = "00",
                desc = "success"
            },
            signature = "" // In Sandbox/Test gateway or simulated webhook
        };

        var now = clock.UtcNow;
        var webhookPayload = new
        {
            eventId = eventId,
            merchantReference = topupReference,
            providerTransactionId = $"TX-{eventId}",
            amount = 120000m,
            currency = "VND",
            status = "Succeeded"
        };
        var rawBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(webhookPayload));
        var signature = SandboxPaymentGateway.ComputeHmac(rawBody, SandboxPaymentGateway.DefaultSecret);

        var headers = new Dictionary<string, string>
        {
            [SandboxPaymentGateway.SignatureHeader] = signature,
            [SandboxPaymentGateway.TimestampHeader] = now.ToString("O")
        };

        var command = new ProcessPaymentWebhookCommand("Sandbox", rawBody, headers);
        var result = await webhookHandler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Acknowledged);

        // Verify user received 10 purchased credits from Gói Sáng Tạo
        var updatedAccount = await db.CreditAccounts.SingleAsync(a => a.UserId == userId);
        Assert.Equal(10, updatedAccount.PurchasedBalance);
        Assert.Equal(1, updatedAccount.TrialBalance);
    }
}
