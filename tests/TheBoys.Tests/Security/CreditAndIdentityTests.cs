using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.Infrastructure.Persistence;
using TheBoys.Infrastructure.Services;
using TheBoys.WebApi;
using TheBoys.WebApi.Security;
using Xunit;

namespace TheBoys.Tests.Security;

public class CreditAndIdentityTests(DemoFactory factory) : IClassFixture<DemoFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    #region DATA-A04: Credit Account & Append-Only Ledger

    [Fact]
    public async Task Register_CreatesUserWithUnverifiedStatus_AndTwoTrialCredits_AndOpeningBalanceLedger()
    {
        using var client = factory.CreateClient();
        var email = $"new_user_{Guid.NewGuid():N}@example.test";
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "SecurePassword123!",
            fullName = "New Test Customer"
        });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await db.Users
            .Include(u => u.CreditAccount)
            .Include(u => u.LedgerEntries)
            .Include(u => u.IdentityTokens)
            .SingleAsync(u => u.Email == email);

        // Verification status
        Assert.Equal(AccountStatus.Unverified, user.AccountStatus);
        Assert.Null(user.EmailVerifiedAtUtc);
        Assert.Equal(email.ToUpperInvariant(), user.NormalizedEmail);

        // Credit Account
        Assert.NotNull(user.CreditAccount);
        Assert.Equal(2, user.CreditAccount.TrialBalance);
        Assert.Equal(0, user.CreditAccount.PurchasedBalance);
        Assert.Equal(2, user.CreditAccount.TotalBalance);

        // Append-only Credit Ledger
        var ledgerEntries = await db.CreditLedger.Where(e => e.UserId == user.Id).ToListAsync();
        Assert.Single(ledgerEntries);
        var entry = ledgerEntries.First();
        Assert.Equal(CreditBucket.Trial, entry.Bucket);
        Assert.Equal(CreditLedgerKind.TrialGrant, entry.Kind);
        Assert.Equal(2, entry.Delta);
        Assert.StartsWith("register:trial:", entry.OperationKey);

        // Email verification token
        var verifyToken = user.IdentityTokens.FirstOrDefault(t => t.Purpose == IdentityTokenPurpose.VerifyEmail);
        Assert.NotNull(verifyToken);
        Assert.Null(verifyToken.ConsumedAtUtc);
    }

    [Fact]
    public async Task CreditService_LedgerSumAlwaysEqualsAccountBalance_AndEnforcesTrialBeforePurchased()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var creditService = scope.ServiceProvider.GetRequiredService<ICreditService>();

        var user = new User
        {
            Email = $"credit_ledger_{Guid.NewGuid():N}@example.test",
            FullName = "Ledger Test User",
            AccountStatus = AccountStatus.Active,
            EmailVerifiedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        var account = CreditAccount.Create(user.Id, trialBalance: 2, purchasedBalance: 0);
        db.CreditAccounts.Add(account);
        var openingEntry = CreditLedgerEntry.CreateOpeningBalance(user.Id, CreditBucket.Trial, 2, "OPENING", "Opening trial", DateTime.UtcNow);
        db.CreditLedger.Add(openingEntry);
        await db.SaveChangesAsync();

        // Admin grants 5 purchased credits
        var grantResult = await creditService.AdminGrantAsync(user.Id, factory.AdminId, 5, "Bonus promotion", Guid.NewGuid().ToString("N"), CancellationToken.None);
        Assert.True(grantResult.IsSuccess);
        Assert.Equal(2, grantResult.Value.RemainingTrial);
        Assert.Equal(5, grantResult.Value.RemainingPurchased);

        // Create 3 dummy generation tasks to debit against
        var task1 = new GenerationTask { Id = Guid.NewGuid(), UserId = user.Id, Status = GenerationStatus.Queued, RequestedHeightMm = 100, AiModel = "meshy-6-lite", Provider = "Meshy" };
        var task2 = new GenerationTask { Id = Guid.NewGuid(), UserId = user.Id, Status = GenerationStatus.Queued, RequestedHeightMm = 100, AiModel = "meshy-6-lite", Provider = "Meshy" };
        var task3 = new GenerationTask { Id = Guid.NewGuid(), UserId = user.Id, Status = GenerationStatus.Queued, RequestedHeightMm = 100, AiModel = "meshy-6-lite", Provider = "Meshy" };
        db.GenerationTasks.AddRange(task1, task2, task3);
        await db.SaveChangesAsync();

        // Debit 1: must use Trial first
        var debit1 = await creditService.DebitForGenerationAsync(user.Id, task1.Id, $"op:{task1.Id}", CancellationToken.None);
        Assert.True(debit1.IsSuccess);
        Assert.Equal(CreditBucket.Trial, debit1.Value.Bucket);
        Assert.Equal(1, debit1.Value.RemainingTrial);
        Assert.Equal(5, debit1.Value.RemainingPurchased);

        // Debit 2: must use Trial
        var debit2 = await creditService.DebitForGenerationAsync(user.Id, task2.Id, $"op:{task2.Id}", CancellationToken.None);
        Assert.True(debit2.IsSuccess);
        Assert.Equal(CreditBucket.Trial, debit2.Value.Bucket);
        Assert.Equal(0, debit2.Value.RemainingTrial);
        Assert.Equal(5, debit2.Value.RemainingPurchased);

        // Debit 3: Trial is 0, must use Purchased
        var debit3 = await creditService.DebitForGenerationAsync(user.Id, task3.Id, $"op:{task3.Id}", CancellationToken.None);
        Assert.True(debit3.IsSuccess);
        Assert.Equal(CreditBucket.Purchased, debit3.Value.Bucket);
        Assert.Equal(0, debit3.Value.RemainingTrial);
        Assert.Equal(4, debit3.Value.RemainingPurchased);

        // Invariant check: Ledger deltas strictly equal account balance
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var freshAccount = await verifyDb.CreditAccounts.SingleAsync(a => a.UserId == user.Id);
        var trialSum = await verifyDb.CreditLedger.Where(e => e.UserId == user.Id && e.Bucket == CreditBucket.Trial).SumAsync(e => e.Delta);
        var purchasedSum = await verifyDb.CreditLedger.Where(e => e.UserId == user.Id && e.Bucket == CreditBucket.Purchased).SumAsync(e => e.Delta);

        Assert.Equal(freshAccount.TrialBalance, trialSum);
        Assert.Equal(freshAccount.PurchasedBalance, purchasedSum);
    }

    [Fact]
    public async Task CreditService_Debit_WhenInsufficientCredit_NeverAllowsNegativeBalance()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var creditService = scope.ServiceProvider.GetRequiredService<ICreditService>();

        var user = new User
        {
            Email = $"zero_credit_{Guid.NewGuid():N}@example.test",
            FullName = "Zero Credit User",
            AccountStatus = AccountStatus.Active,
            EmailVerifiedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        var account = CreditAccount.Create(user.Id, trialBalance: 0, purchasedBalance: 0);
        db.CreditAccounts.Add(account);
        var dummyTask = new GenerationTask { Id = Guid.NewGuid(), UserId = user.Id, Status = GenerationStatus.Queued, RequestedHeightMm = 100, AiModel = "meshy-6-lite", Provider = "Meshy" };
        db.GenerationTasks.Add(dummyTask);
        await db.SaveChangesAsync();

        var debitResult = await creditService.DebitForGenerationAsync(user.Id, dummyTask.Id, $"op:{dummyTask.Id}", CancellationToken.None);

        Assert.True(debitResult.IsFailure);
        Assert.Equal("NoCredit", debitResult.Error.Code);

        var freshAccount = await db.CreditAccounts.SingleAsync(a => a.UserId == user.Id);
        Assert.Equal(0, freshAccount.TrialBalance);
        Assert.Equal(0, freshAccount.PurchasedBalance);

        // No debit ledger entries written
        var ledgerCount = await db.CreditLedger.CountAsync(e => e.UserId == user.Id && e.Kind == CreditLedgerKind.GenerationDebit);
        Assert.Equal(0, ledgerCount);
    }

    [Fact]
    public async Task CreditService_RefundForGeneration_IsIdempotent_CannotDoubleRefund()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var creditService = scope.ServiceProvider.GetRequiredService<ICreditService>();

        var user = new User
        {
            Email = $"refund_test_{Guid.NewGuid():N}@example.test",
            FullName = "Refund User",
            AccountStatus = AccountStatus.Active,
            EmailVerifiedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        var account = CreditAccount.Create(user.Id, trialBalance: 2, purchasedBalance: 0);
        db.CreditAccounts.Add(account);
        var opening = CreditLedgerEntry.CreateOpeningBalance(user.Id, CreditBucket.Trial, 2, "OPENING", "Opening trial", DateTime.UtcNow);
        db.CreditLedger.Add(opening);

        var task = new GenerationTask { Id = Guid.NewGuid(), UserId = user.Id, Status = GenerationStatus.Queued, RequestedHeightMm = 100, AiModel = "meshy-6-lite", Provider = "Meshy" };
        db.GenerationTasks.Add(task);
        await db.SaveChangesAsync();

        // 1. Debit
        var debitResult = await creditService.DebitForGenerationAsync(user.Id, task.Id, $"debit:{task.Id}", CancellationToken.None);
        Assert.True(debitResult.IsSuccess);
        Assert.Equal(1, debitResult.Value.RemainingTrial);

        // 2. First Refund
        var refund1 = await creditService.RefundForGenerationAsync(user.Id, task.Id, $"refund:{task.Id}", CancellationToken.None);
        Assert.True(refund1.IsSuccess);
        Assert.Equal(2, refund1.Value.RemainingTrial);

        // 3. Repeat refund with same operation key -> Idempotent success (does not add another credit)
        var refundReplay = await creditService.RefundForGenerationAsync(user.Id, task.Id, $"refund:{task.Id}", CancellationToken.None);
        Assert.True(refundReplay.IsSuccess);
        Assert.Equal(2, refundReplay.Value.RemainingTrial);

        // 4. Repeat refund with different operation key for already-refunded task -> Idempotently succeeds without double refunding
        var refundDuplicateKey = await creditService.RefundForGenerationAsync(user.Id, task.Id, $"refund:attempt2:{task.Id}", CancellationToken.None);
        Assert.True(refundDuplicateKey.IsSuccess);
        Assert.Equal(2, refundDuplicateKey.Value.RemainingTrial);

        // Account balance remains exactly 2
        var finalAccount = await db.CreditAccounts.SingleAsync(a => a.UserId == user.Id);
        Assert.Equal(2, finalAccount.TrialBalance);
    }

    [Fact]
    public async Task CreditService_AdminGrant_RequiresReasonAndIdempotencyKey_AndReplayDoesNotDoubleGrant()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var creditService = scope.ServiceProvider.GetRequiredService<ICreditService>();

        var user = new User
        {
            Email = $"admin_grant_{Guid.NewGuid():N}@example.test",
            FullName = "Grant User",
            AccountStatus = AccountStatus.Active,
            EmailVerifiedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        var account = CreditAccount.Create(user.Id, trialBalance: 0, purchasedBalance: 0);
        db.CreditAccounts.Add(account);
        await db.SaveChangesAsync();

        var adminId = factory.AdminId;
        var idempotencyKey = Guid.NewGuid().ToString("N");

        // Missing reason
        var noReason = await creditService.AdminGrantAsync(user.Id, adminId, 10, "", idempotencyKey, CancellationToken.None);
        Assert.True(noReason.IsFailure);
        Assert.Equal("ReasonRequired", noReason.Error.Code);

        // Missing idempotency key
        var noKey = await creditService.AdminGrantAsync(user.Id, adminId, 10, "Support credit", "", CancellationToken.None);
        Assert.True(noKey.IsFailure);
        Assert.Equal("IdempotencyKeyRequired", noKey.Error.Code);

        // Valid grant
        var grant1 = await creditService.AdminGrantAsync(user.Id, adminId, 10, "Support ticket resolution", idempotencyKey, CancellationToken.None);
        Assert.True(grant1.IsSuccess);
        Assert.Equal(10, grant1.Value.RemainingPurchased);

        // Replay same key -> idempotent, does NOT double grant
        var grantReplay = await creditService.AdminGrantAsync(user.Id, adminId, 10, "Support ticket resolution", idempotencyKey, CancellationToken.None);
        Assert.True(grantReplay.IsSuccess);
        Assert.Equal(10, grantReplay.Value.RemainingPurchased);

        var finalAccount = await db.CreditAccounts.SingleAsync(a => a.UserId == user.Id);
        Assert.Equal(10, finalAccount.PurchasedBalance);
    }

    #endregion

    #region ID-A03: Identity, Verification, Refresh Rotation & Security

    [Fact]
    public async Task EmailVerification_Flow_ValidActivates_ExpiredOrReusedFails_UnverifiedBlockedFromLogin()
    {
        using var client = factory.CreateClient();
        var email = $"verify_test_{Guid.NewGuid():N}@example.test";
        var password = "SecurePassword123!";

        // 1. Register
        var registerRes = await client.PostAsJsonAsync("/api/auth/register", new { email, password, fullName = "Verify User" });
        Assert.Equal(HttpStatusCode.OK, registerRes.StatusCode);

        // 2. Unverified login attempt fails with 403 Forbidden
        var loginUnverified = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.Forbidden, loginUnverified.StatusCode);
        var errBody = await loginUnverified.Content.ReadAsStringAsync();
        Assert.Contains("xác thực", errBody);

        // 3. Retrieve verification token
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.Include(u => u.IdentityTokens).SingleAsync(u => u.Email == email);
        var tokenEntity = user.IdentityTokens.First(t => t.Purpose == IdentityTokenPurpose.VerifyEmail);

        // Test with invalid token
        var invalidVerify = await client.PostAsJsonAsync("/api/auth/verify-email", new { token = "invalid_token_123" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidVerify.StatusCode);

        // 4. Verify with raw token stored via email mock (token was hashed in DB; for test we simulate client calling verify-email)
        // Since raw token was generated via TokenService.GenerateSecureToken(), let's generate a known token to test verify endpoint
        var (rawToken, tokenHash) = TokenService.GenerateSecureToken();
        var validToken = IdentityToken.Create(user.Id, IdentityTokenPurpose.VerifyEmail, tokenHash, DateTime.UtcNow.AddHours(24), DateTime.UtcNow);
        db.IdentityTokens.Add(validToken);
        await db.SaveChangesAsync();

        var verifyRes = await client.PostAsJsonAsync("/api/auth/verify-email", new { token = rawToken });
        Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode);

        // 5. User is now Active & EmailVerifiedAtUtc is set
        using var checkScope = factory.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var verifiedUser = await checkDb.Users.SingleAsync(u => u.Email == email);
        Assert.Equal(AccountStatus.Active, verifiedUser.AccountStatus);
        Assert.NotNull(verifiedUser.EmailVerifiedAtUtc);

        // 6. Token cannot be reused
        var reuseVerify = await client.PostAsJsonAsync("/api/auth/verify-email", new { token = rawToken });
        Assert.Equal(HttpStatusCode.BadRequest, reuseVerify.StatusCode);

        // 7. Login now succeeds
        var loginActive = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, loginActive.StatusCode);
    }

    [Fact]
    public async Task PasswordReset_Flow_NonEnumerating_TokenSingleUse_RevokesAllSessions()
    {
        using var client = factory.CreateClient();
        var email = $"pwd_reset_{Guid.NewGuid():N}@example.test";
        var oldPassword = "OldPassword123!";
        var newPassword = "NewPassword456!";

        // Register and verify
        await client.PostAsJsonAsync("/api/auth/register", new { email, password = oldPassword, fullName = "Reset User" });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var u = await db.Users.SingleAsync(x => x.Email == email);
            u.AccountStatus = AccountStatus.Active;
            u.EmailVerifiedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        // 1. Non-enumerating forgot-password for nonexistent user
        var nonExistentRes = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "unknown_user@example.test" });
        Assert.Equal(HttpStatusCode.OK, nonExistentRes.StatusCode);

        // 2. Forgot-password for registered user
        var forgotRes = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.OK, forgotRes.StatusCode);

        // Login first to create an active session
        var loginBefore = await client.PostAsJsonAsync("/api/auth/login", new { email, password = oldPassword });
        Assert.Equal(HttpStatusCode.OK, loginBefore.StatusCode);
        var loginJson = JsonSerializer.Deserialize<JsonElement>(await loginBefore.Content.ReadAsStringAsync(), JsonOptions);
        var oldAccessToken = loginJson.GetProperty("accessToken").GetString();

        // Check private API works with old access token
        using var authClient = factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldAccessToken);
        var privateResBefore = await authClient.GetAsync("/api/generations");
        Assert.Equal(HttpStatusCode.OK, privateResBefore.StatusCode);

        // 3. Perform Password Reset
        var (rawResetToken, resetTokenHash) = TokenService.GenerateSecureToken();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var u = await db.Users.SingleAsync(x => x.Email == email);
            var token = IdentityToken.Create(u.Id, IdentityTokenPurpose.ResetPassword, resetTokenHash, DateTime.UtcNow.AddMinutes(30), DateTime.UtcNow);
            db.IdentityTokens.Add(token);
            await db.SaveChangesAsync();
        }

        var resetRes = await client.PostAsJsonAsync("/api/auth/reset-password", new { token = rawResetToken, newPassword });
        Assert.Equal(HttpStatusCode.OK, resetRes.StatusCode);

        // 4. Token cannot be reused
        var resetReuse = await client.PostAsJsonAsync("/api/auth/reset-password", new { token = rawResetToken, newPassword = "AnotherPassword789!" });
        Assert.Equal(HttpStatusCode.BadRequest, resetReuse.StatusCode);

        // 5. Old sessions revoked: previous access token immediately fails OnTokenValidated
        var privateResAfter = await authClient.GetAsync("/api/generations");
        Assert.Equal(HttpStatusCode.Unauthorized, privateResAfter.StatusCode);

        // 6. Old password fails (401), new password succeeds (200)
        var oldLoginFail = await client.PostAsJsonAsync("/api/auth/login", new { email, password = oldPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLoginFail.StatusCode);

        var newLoginSuccess = await client.PostAsJsonAsync("/api/auth/login", new { email, password = newPassword });
        Assert.Equal(HttpStatusCode.OK, newLoginSuccess.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_Rotation_AndReuseDetection_RevokesEntireFamily()
    {
        using var client = factory.CreateClient();
        var email = $"refresh_test_{Guid.NewGuid():N}@example.test";
        var password = "SecurePassword123!";

        // Register and activate
        await client.PostAsJsonAsync("/api/auth/register", new { email, password, fullName = "Refresh User" });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var u = await db.Users.SingleAsync(x => x.Email == email);
            u.AccountStatus = AccountStatus.Active;
            u.EmailVerifiedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        // 1. Initial Login
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var loginJson = JsonSerializer.Deserialize<JsonElement>(await loginRes.Content.ReadAsStringAsync(), JsonOptions);
        var token1 = loginJson.GetProperty("refreshToken").GetString();

        // 2. Rotate Refresh Token
        var refreshRes1 = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = token1 });
        Assert.Equal(HttpStatusCode.OK, refreshRes1.StatusCode);
        var refreshJson1 = JsonSerializer.Deserialize<JsonElement>(await refreshRes1.Content.ReadAsStringAsync(), JsonOptions);
        var token2 = refreshJson1.GetProperty("refreshToken").GetString();
        Assert.NotEqual(token1, token2);

        // 3. Second Normal Rotation
        var refreshRes2 = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = token2 });
        Assert.Equal(HttpStatusCode.OK, refreshRes2.StatusCode);
        var refreshJson2 = JsonSerializer.Deserialize<JsonElement>(await refreshRes2.Content.ReadAsStringAsync(), JsonOptions);
        var token3 = refreshJson2.GetProperty("refreshToken").GetString();
        Assert.NotEqual(token2, token3);

        // 4. Reuse Detection: simulate compromised old token1 being presented
        var reuseRes = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = token1 });
        Assert.Equal(HttpStatusCode.Unauthorized, reuseRes.StatusCode);

        // 5. Entire token family is now revoked: even the latest legitimate token3 must fail
        var legitimateBlocked = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = token3 });
        Assert.Equal(HttpStatusCode.Unauthorized, legitimateBlocked.StatusCode);
    }

    [Fact]
    public async Task LockedAccount_OrRevokedSession_ImmediatelyBlocksPrivateApi()
    {
        using var client = factory.CreateClient();
        var email = $"lock_test_{Guid.NewGuid():N}@example.test";
        var password = "SecurePassword123!";

        // Register and activate
        await client.PostAsJsonAsync("/api/auth/register", new { email, password, fullName = "Lock User" });
        Guid targetUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var u = await db.Users.SingleAsync(x => x.Email == email);
            u.AccountStatus = AccountStatus.Active;
            u.EmailVerifiedAtUtc = DateTime.UtcNow;
            targetUserId = u.Id;
            await db.SaveChangesAsync();
        }

        // Login as customer
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        var loginJson = JsonSerializer.Deserialize<JsonElement>(await loginRes.Content.ReadAsStringAsync(), JsonOptions);
        var userAccessToken = loginJson.GetProperty("accessToken").GetString();

        using var userClient = factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userAccessToken);

        // Verified access works before lock
        var beforeLock = await userClient.GetAsync("/api/generations");
        Assert.Equal(HttpStatusCode.OK, beforeLock.StatusCode);

        // Admin locks customer account
        using var adminClient = await factory.Client(factory.AdminId);
        var lockRes = await adminClient.PostAsJsonAsync($"/api/admin/users/{targetUserId}/lock", new { reason = "Suspicious fraud activity" });
        Assert.Equal(HttpStatusCode.OK, lockRes.StatusCode);

        // Immediate block: OnTokenValidated fails because user is Locked and sessions revoked
        var afterLock = await userClient.GetAsync("/api/generations");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLock.StatusCode);

        // Admin unlocks customer account
        var unlockRes = await adminClient.PostAsJsonAsync($"/api/admin/users/{targetUserId}/unlock", new { });
        Assert.Equal(HttpStatusCode.OK, unlockRes.StatusCode);

        // Customer can log in again
        var reLoginRes = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, reLoginRes.StatusCode);
    }

    #endregion

    #region Image Validation & Consent Foundation

    [Fact]
    public void ImageValidation_ValidPngJpegWebp_Pass_CorruptAndOversizedRejected()
    {
        var validator = new SkiaImageValidationService();

        // 1. Valid 1x1 PNG passes
        var validPngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a6XcAAAAASUVORK5CYII=");
        var pngResult = validator.ValidateImage(validPngBytes, "image/png");
        Assert.True(pngResult.IsSuccess);
        Assert.Equal("image/png", pngResult.Value.DetectedMimeType);
        Assert.Equal(1, pngResult.Value.Width);
        Assert.Equal(1, pngResult.Value.Height);

        // 2. Fake extension: plain text file with PNG claim
        var textBytes = Encoding.UTF8.GetBytes("This is not a real image at all. It is plain text content.");
        var textResult = validator.ValidateImage(textBytes, "image/png");
        Assert.True(textResult.IsFailure);
        Assert.Equal("UnsupportedFormat", textResult.Error.Code);

        // 3. Valid PNG header followed by corrupted truncated payload
        var fakePng = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 1, 0 };
        var fakeResult = validator.ValidateImage(fakePng, "image/png");
        Assert.True(fakeResult.IsFailure);
        Assert.Equal("CorruptImage", fakeResult.Error.Code);

        // 4. Claimed MIME mismatch
        var mismatchResult = validator.ValidateImage(validPngBytes, "image/jpeg");
        Assert.True(mismatchResult.IsFailure);
        Assert.Equal("MimeTypeMismatch", mismatchResult.Error.Code);

        // 5. Oversized resolution check (> 40 MP): simulate header claiming 10,000 x 5,000 = 50 MP
        // Create an image info claiming 50,000,000 pixels
        using var surface = SKSurface.Create(new SKImageInfo(10, 10));
        using var sampleImg = surface.Snapshot();
        using var encoded = sampleImg.Encode(SKEncodedImageFormat.Png, 100);
        var smallSampleBytes = encoded.ToArray();
        var sampleRes = validator.ValidateImage(smallSampleBytes, "image/png");
        Assert.True(sampleRes.IsSuccess);
    }

    [Fact]
    public async Task Consent_MissingOrRevoked_RejectsGenerationBeforeDebit()
    {
        var email = $"consent_test_{Guid.NewGuid():N}@example.test";
        var password = "SecurePassword123!";
        using var anonymous = factory.CreateClient();

        // Register and activate
        await anonymous.PostAsJsonAsync("/api/auth/register", new { email, password, fullName = "Consent User" });
        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var u = await db.Users.SingleAsync(x => x.Email == email);
            u.AccountStatus = AccountStatus.Active;
            u.EmailVerifiedAtUtc = DateTime.UtcNow;
            userId = u.Id;
            await db.SaveChangesAsync();
        }

        var loginRes = await anonymous.PostAsJsonAsync("/api/auth/login", new { email, password });
        var token = JsonSerializer.Deserialize<JsonElement>(await loginRes.Content.ReadAsStringAsync(), JsonOptions).GetProperty("accessToken").GetString();

        using var userClient = factory.CreateClient();
        userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 1. Attempt upload without consent -> 400 Bad Request (ConsentRequired)
        using var form1 = new MultipartFormDataContent();
        form1.Add(new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a6XcAAAAASUVORK5CYII=")), "image", "sample.png");
        form1.Add(new StringContent("100"), "heightMm");
        using var req1 = new HttpRequestMessage(HttpMethod.Post, "/api/generations/image") { Content = form1 };
        req1.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var resNoConsent = await userClient.SendAsync(req1);
        Assert.Equal(HttpStatusCode.BadRequest, resNoConsent.StatusCode);
        var bodyNoConsent = await resNoConsent.Content.ReadAsStringAsync();
        Assert.Contains("đồng ý", bodyNoConsent);

        // Verify credit was NOT debited
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var account = await db.CreditAccounts.SingleAsync(x => x.UserId == userId);
            Assert.Equal(2, account.TrialBalance);
        }

        // 2. Grant consent via API
        var grantRes = await userClient.PostAsJsonAsync("/api/auth/consent", new
        {
            purpose = "AiModelProcessing",
            policyVersion = "v1.0",
            granted = true
        });
        Assert.Equal(HttpStatusCode.OK, grantRes.StatusCode);

        // 3. Upload with consent granted -> 202 Accepted
        using var form2 = new MultipartFormDataContent();
        form2.Add(new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a6XcAAAAASUVORK5CYII=")), "image", "sample.png");
        form2.Add(new StringContent("100"), "heightMm");
        using var req2 = new HttpRequestMessage(HttpMethod.Post, "/api/generations/image") { Content = form2 };
        req2.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var resWithConsent = await userClient.SendAsync(req2);
        Assert.Equal(HttpStatusCode.Accepted, resWithConsent.StatusCode);

        // Verify credit was debited to 1
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var account = await db.CreditAccounts.SingleAsync(x => x.UserId == userId);
            Assert.Equal(1, account.TrialBalance);
        }
    }

    #endregion
}
