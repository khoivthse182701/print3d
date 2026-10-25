using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TheBoys.Application.Common.Interfaces;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;
using TheBoys.WebApi.Security;

namespace TheBoys.WebApi.Controllers;

public sealed class GoogleLoginRequest
{
    public string? IdToken { get; set; }
    public string? AccessToken { get; set; }
}

public sealed class GoogleUserInfoPayload
{
    [System.Text.Json.Serialization.JsonPropertyName("email")]
    public string? Email { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("given_name")]
    public string? GivenName { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("email_verified")]
    public bool? EmailVerified { get; set; }
}

public sealed class RegisterRequest
{
    [Required, EmailAddress, MaxLength(255)] public string Email { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 10)] public string Password { get; set; } = "";
    [Required, StringLength(150, MinimumLength = 1)] public string FullName { get; set; } = "";
}

public sealed class LoginRequest
{
    [Required, EmailAddress, MaxLength(255)] public string Email { get; set; } = "";
    [Required, MaxLength(128)] public string Password { get; set; } = "";
}

public sealed class VerifyEmailRequest
{
    [Required] public string Token { get; set; } = "";
}

public sealed class ResendVerificationRequest
{
    [Required, EmailAddress, MaxLength(255)] public string Email { get; set; } = "";
}

public sealed class RefreshTokenRequest
{
    public string? RefreshToken { get; set; }
}

public sealed class ForgotPasswordRequest
{
    [Required, EmailAddress, MaxLength(255)] public string Email { get; set; } = "";
}

public sealed class ResetPasswordRequest
{
    [Required] public string Token { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 10)] public string NewPassword { get; set; } = "";
}

public sealed class ConsentRequest
{
    [Required, MaxLength(40)] public string Purpose { get; set; } = "AiModelProcessing";
    [Required, MaxLength(40)] public string PolicyVersion { get; set; } = "v1.0";
    public bool Granted { get; set; } = true;
}

[ApiController, Route("api/auth")]
public sealed class AuthController(
    IApplicationDbContext db,
    IPasswordHasher<User> hasher,
    TokenService tokens,
    IEmailSender emailSender,
    IConsentService consentService,
    IAuditService auditService,
    IClock clock,
    IHostEnvironment environment,
    IConfiguration config) : ApiControllerBase
{
    [HttpPost("register"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var normalizedEmail = email.ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest(new { message = "Tên không hợp lệ." });

        if (await db.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail, ct))
            return Conflict(new { message = "Email đã được sử dụng." });

        var user = new User
        {
            Email = email,
            NormalizedEmail = normalizedEmail,
            FullName = request.FullName.Trim(),
            Role = UserRole.Customer,
            AccountStatus = AccountStatus.Unverified
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password);

        var creditAccount = CreditAccount.Create(user.Id, trialBalance: 1, purchasedBalance: 0);
        user.CreditAccount = creditAccount;

        var ledgerEntry = CreditLedgerEntry.CreateTrialGrant(
            user.Id,
            1,
            $"register:trial:{user.Id}",
            null,
            "Khởi tạo tài khoản dùng thử 1 lượt tạo 3D",
            clock.UtcNow);

        // Generate email verification OTP code (hash stored only)
        var (rawToken, tokenHash) = TokenService.GenerateOtpCode(6);
        var identityToken = IdentityToken.Create(
            user.Id,
            IdentityTokenPurpose.VerifyEmail,
            tokenHash,
            clock.UtcNow.AddHours(24),
            clock.UtcNow);

        await using var tx = await db.BeginTransactionAsync(ct);
        db.Users.Add(user);
        db.CreditAccounts.Add(creditAccount);
        db.CreditLedger.Add(ledgerEntry);
        db.IdentityTokens.Add(identityToken);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await emailSender.SendVerificationEmailAsync(user.Email, user.FullName, rawToken, ct);

        await auditService.RecordAsync(
            user.Id,
            "Auth.Register",
            "User",
            user.Id,
            Guid.NewGuid().ToString("N"),
            new { user.Email, user.FullName },
            "Đăng ký tài khoản khách hàng mới",
            ct);

        return Ok(new
        {
            message = "Đăng ký thành công. Vui lòng kiểm tra email để xác thực tài khoản.",
            userId = user.Id,
            email = user.Email
        });
    }

    [HttpPost("verify-email"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { message = "Token xác thực không được rỗng." });

        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.Token.Trim().ToLowerInvariant()));

        var tokenEntity = await db.IdentityTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash && x.Purpose == IdentityTokenPurpose.VerifyEmail, ct);

        if (tokenEntity == null || !tokenEntity.IsValid(clock.UtcNow))
            return BadRequest(new { message = "Mã xác thực không hợp lệ hoặc đã hết hạn." });

        tokenEntity.Consume(clock.UtcNow);
        tokenEntity.User!.VerifyEmail(clock.UtcNow);

        await db.SaveChangesAsync(ct);

        await auditService.RecordAsync(
            tokenEntity.UserId,
            "Auth.VerifyEmail",
            "User",
            tokenEntity.UserId,
            Guid.NewGuid().ToString("N"),
            new { },
            "Xác thực địa chỉ email thành công",
            ct);

        return Ok(new { message = "Xác thực email thành công. Bạn có thể đăng nhập ngay bây giờ." });
    }

    [HttpPost("resend-verification"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequest request, CancellationToken ct)
    {
        // Always return generic response to prevent user enumeration
        var genericResponse = Ok(new { message = "Nếu email tồn tại trên hệ thống và chưa xác thực, mã xác nhận mới đã được gửi." });

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, ct);

        if (user == null || user.AccountStatus != AccountStatus.Unverified)
            return genericResponse;

        // Invalidate prior unconsumed tokens
        var existingTokens = await db.IdentityTokens
            .Where(x => x.UserId == user.Id && x.Purpose == IdentityTokenPurpose.VerifyEmail && x.ConsumedAtUtc == null)
            .ToListAsync(ct);
        foreach (var t in existingTokens) t.Invalidate(clock.UtcNow);

        var (rawToken, tokenHash) = TokenService.GenerateOtpCode(6);
        var identityToken = IdentityToken.Create(
            user.Id,
            IdentityTokenPurpose.VerifyEmail,
            tokenHash,
            clock.UtcNow.AddHours(24),
            clock.UtcNow);

        db.IdentityTokens.Add(identityToken);
        await db.SaveChangesAsync(ct);

        await emailSender.SendVerificationEmailAsync(user.Email, user.FullName, rawToken, ct);

        return genericResponse;
    }

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var user = await db.Users
            .Include(u => u.CreditAccount)
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, ct);

        if (user?.PasswordHash == null)
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng." });

        if (user.IsLockedOut(clock.UtcNow))
            return StatusCode(StatusCodes.Status423Locked, new { message = "Tài khoản tạm thời bị khóa do nhập sai nhiều lần. Vui lòng thử lại sau." });

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            user.RecordFailedLogin(maxFailedAttempts: 5, lockoutMinutes: 15, clock.UtcNow);
            await db.SaveChangesAsync(ct);
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng." });
        }

        if (user.AccountStatus == AccountStatus.Unverified)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Tài khoản chưa được xác thực email. Vui lòng xác thực trước khi đăng nhập." });

        if (Roles.Name(user.Role) == "disabled" || user.AccountStatus == AccountStatus.Anonymized)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Tài khoản không khả dụng." });

        user.ResetFailedLogins();

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
        }

        // Create refresh session with hashed token and family
        var (rawRefreshToken, refreshTokenHash) = TokenService.GenerateSecureToken();
        var familyId = Guid.NewGuid();
        var expiresAtUtc = clock.UtcNow.AddDays(7);

        var session = RefreshSession.Create(
            user.Id,
            familyId,
            refreshTokenHash,
            expiresAtUtc,
            clock.UtcNow);

        db.RefreshSessions.Add(session);
        await db.SaveChangesAsync(ct);

        var (accessToken, accessExpires) = tokens.CreateAccessToken(user, session.Id);

        SetRefreshTokenCookie(rawRefreshToken, expiresAtUtc);

        return Ok(tokens.CreateResponse(user, accessToken, accessExpires, rawRefreshToken));
    }

    [HttpPost("google-login"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken) && string.IsNullOrWhiteSpace(request.AccessToken))
            return BadRequest(new { message = "Google Token không được để trống." });

        string email;
        string fullName;

        if (!string.IsNullOrWhiteSpace(request.AccessToken))
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.AccessToken);
                var response = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v3/userinfo", ct);
                if (!response.IsSuccessStatusCode)
                {
                    return Unauthorized(new { message = "Google Access Token không hợp lệ hoặc đã hết hạn." });
                }

                var userInfo = await response.Content.ReadFromJsonAsync<GoogleUserInfoPayload>(ct);
                if (userInfo == null || string.IsNullOrWhiteSpace(userInfo.Email))
                {
                    return Unauthorized(new { message = "Không tìm thấy thông tin email trong tài khoản Google." });
                }

                email = userInfo.Email.Trim().ToLowerInvariant();
                fullName = string.IsNullOrWhiteSpace(userInfo.Name)
                    ? (userInfo.GivenName ?? email.Split('@')[0])
                    : userInfo.Name.Trim();
            }
            catch (Exception ex)
            {
                return Unauthorized(new { message = $"Xác thực Google Access Token thất bại: {ex.Message}" });
            }
        }
        else
        {
            var googleClientId = config["GoogleAuthSettings:ClientId"]
                ?? "928800899912-81ire3k9j09sibcegq1hriqgg231ahqr.apps.googleusercontent.com";

            GoogleJsonWebSignature.Payload payload;
            try
            {
                var validationSettings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { googleClientId }
                };
                payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, validationSettings);
            }
            catch (InvalidJwtException ex)
            {
                return Unauthorized(new { message = $"Google ID Token không hợp lệ hoặc đã hết hạn: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { message = $"Xác thực Google ID Token thất bại: {ex.Message}" });
            }

            if (payload == null || string.IsNullOrWhiteSpace(payload.Email))
            {
                return Unauthorized(new { message = "Không tìm thấy thông tin email trong Google ID Token." });
            }

            email = payload.Email.Trim().ToLowerInvariant();
            fullName = string.IsNullOrWhiteSpace(payload.Name)
                ? (payload.GivenName ?? email.Split('@')[0])
                : payload.Name.Trim();
        }

        var normalizedEmail = email.ToUpperInvariant();

        var user = await db.Users
            .Include(u => u.CreditAccount)
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, ct);

        if (user == null)
        {
            user = new User
            {
                Email = email,
                NormalizedEmail = normalizedEmail,
                FullName = fullName,
                Role = UserRole.Customer,
                AccountStatus = AccountStatus.Active,
                EmailVerifiedAtUtc = clock.UtcNow
            };
            user.PasswordHash = hasher.HashPassword(user, Guid.NewGuid().ToString("N"));

            var creditAccount = CreditAccount.Create(user.Id, trialBalance: 2, purchasedBalance: 0);
            user.CreditAccount = creditAccount;

            var ledgerEntry = CreditLedgerEntry.CreateTrialGrant(
                user.Id,
                2,
                $"register:google:{user.Id}",
                null,
                "Khởi tạo tài khoản Google dùng thử 2 lượt tạo 3D",
                clock.UtcNow);

            await using var tx = await db.BeginTransactionAsync(ct);
            db.Users.Add(user);
            db.CreditAccounts.Add(creditAccount);
            db.CreditLedger.Add(ledgerEntry);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await auditService.RecordAsync(
                user.Id,
                "Auth.GoogleRegister",
                "User",
                user.Id,
                Guid.NewGuid().ToString("N"),
                new { user.Email, user.FullName },
                "Đăng ký tài khoản mới tự động qua Google Login",
                ct);
        }
        else
        {
            if (user.IsLockedOut(clock.UtcNow))
                return StatusCode(StatusCodes.Status423Locked, new { message = "Tài khoản tạm thời bị khóa. Vui lòng liên hệ quản trị viên." });

            if (user.AccountStatus == AccountStatus.Locked || user.AccountStatus == AccountStatus.Anonymized || Roles.Name(user.Role) == "disabled")
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Tài khoản không khả dụng." });

            if (user.AccountStatus == AccountStatus.Unverified)
            {
                user.VerifyEmail(clock.UtcNow);
            }

            if (user.CreditAccount == null)
            {
                var creditAccount = CreditAccount.Create(user.Id, trialBalance: 2, purchasedBalance: 0);
                user.CreditAccount = creditAccount;
                db.CreditAccounts.Add(creditAccount);
            }

            user.ResetFailedLogins();
            await db.SaveChangesAsync(ct);

            await auditService.RecordAsync(
                user.Id,
                "Auth.GoogleLogin",
                "User",
                user.Id,
                Guid.NewGuid().ToString("N"),
                new { user.Email },
                "Đăng nhập thành công qua Google",
                ct);
        }

        var (rawRefreshToken, refreshTokenHash) = TokenService.GenerateSecureToken();
        var familyId = Guid.NewGuid();
        var expiresAtUtc = clock.UtcNow.AddDays(7);

        var session = RefreshSession.Create(
            user.Id,
            familyId,
            refreshTokenHash,
            expiresAtUtc,
            clock.UtcNow);

        db.RefreshSessions.Add(session);
        await db.SaveChangesAsync(ct);

        var (accessToken, accessExpires) = tokens.CreateAccessToken(user, session.Id);

        SetRefreshTokenCookie(rawRefreshToken, expiresAtUtc);

        return Ok(tokens.CreateResponse(user, accessToken, accessExpires, rawRefreshToken));
    }

    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request, CancellationToken ct)
    {
        var rawToken = request.RefreshToken;
        if (string.IsNullOrWhiteSpace(rawToken) && Request.Cookies.TryGetValue("refreshToken", out var cookieToken))
        {
            rawToken = cookieToken;
        }

        if (string.IsNullOrWhiteSpace(rawToken))
            return Unauthorized(new { message = "Refresh token không được để trống." });

        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken.Trim().ToLowerInvariant()));

        var session = await db.RefreshSessions
            .Include(s => s.User)
            .ThenInclude(u => u!.CreditAccount)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

        if (session == null)
            return Unauthorized(new { message = "Phiên làm việc không tồn tại." });

        // REUSE DETECTION: if token is already revoked, compromise detected! Revoke whole family!
        if (session.RevokedAtUtc != null)
        {
            var familySessions = await db.RefreshSessions
                .Where(x => x.FamilyId == session.FamilyId && x.RevokedAtUtc == null)
                .ToListAsync(ct);

            foreach (var s in familySessions) s.Revoke(clock.UtcNow);
            await db.SaveChangesAsync(ct);

            await auditService.RecordAsync(
                session.UserId,
                "Auth.RefreshTokenReuseDetected",
                "RefreshSession",
                session.Id,
                Guid.NewGuid().ToString("N"),
                new { session.FamilyId },
                "Phát hiện dùng lại refresh token cũ. Đã thu hồi toàn bộ phiên trong family.",
                ct);

            DeleteRefreshTokenCookie();
            return Unauthorized(new { message = "Phát hiện phiên đăng nhập bất thường. Toàn bộ phiên liên quan đã bị thu hồi." });
        }

        if (session.ExpiresAtUtc <= clock.UtcNow)
            return Unauthorized(new { message = "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại." });

        if (session.User == null || session.User.IsLockedOut(clock.UtcNow) || Roles.Name(session.User.Role) == "disabled")
            return Unauthorized(new { message = "Tài khoản không khả dụng." });

        // Rotate: issue new refresh session within the same family, replace old one
        var (newRawToken, newTokenHash) = TokenService.GenerateSecureToken();
        var newExpires = clock.UtcNow.AddDays(7);

        var newSession = RefreshSession.Create(
            session.UserId,
            session.FamilyId,
            newTokenHash,
            newExpires,
            clock.UtcNow);

        db.RefreshSessions.Add(newSession);
        session.Replace(newSession.Id, clock.UtcNow);

        await db.SaveChangesAsync(ct);

        var (accessToken, accessExpires) = tokens.CreateAccessToken(session.User, newSession.Id);

        SetRefreshTokenCookie(newRawToken, newExpires);

        return Ok(tokens.CreateResponse(session.User, accessToken, accessExpires, newRawToken));
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var sidStr = User.FindFirstValue("sid");
        if (Guid.TryParse(sidStr, out var sessionId))
        {
            var session = await db.RefreshSessions.SingleOrDefaultAsync(x => x.Id == sessionId && x.UserId == UserId, ct);
            session?.Revoke(clock.UtcNow);
            await db.SaveChangesAsync(ct);
        }

        DeleteRefreshTokenCookie();
        return Ok(new { message = "Đăng xuất thành công." });
    }

    [HttpPost("revoke-all"), Authorize]
    public async Task<IActionResult> RevokeAll(CancellationToken ct)
    {
        var sessions = await db.RefreshSessions
            .Where(x => x.UserId == UserId && x.RevokedAtUtc == null)
            .ToListAsync(ct);

        foreach (var s in sessions) s.Revoke(clock.UtcNow);
        await db.SaveChangesAsync(ct);

        DeleteRefreshTokenCookie();

        await auditService.RecordAsync(
            UserId,
            "Auth.RevokeAllSessions",
            "User",
            UserId,
            Guid.NewGuid().ToString("N"),
            new { count = sessions.Count },
            "Thu hồi toàn bộ phiên đăng nhập của người dùng",
            ct);

        return Ok(new { message = "Đã thu hồi toàn bộ phiên đăng nhập thành công." });
    }

    [HttpPost("forgot-password"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        // Non-enumerating response
        var genericResponse = Ok(new { message = "Nếu email tồn tại trên hệ thống, hướng dẫn đặt lại mật khẩu đã được gửi." });

        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, ct);

        if (user == null || user.AccountStatus == AccountStatus.Locked || user.AccountStatus == AccountStatus.Anonymized)
            return genericResponse;

        // Invalidate prior unconsumed reset tokens
        var existingTokens = await db.IdentityTokens
            .Where(x => x.UserId == user.Id && x.Purpose == IdentityTokenPurpose.ResetPassword && x.ConsumedAtUtc == null)
            .ToListAsync(ct);
        foreach (var t in existingTokens) t.Invalidate(clock.UtcNow);

        var (rawToken, tokenHash) = TokenService.GenerateOtpCode(6);
        var resetToken = IdentityToken.Create(
            user.Id,
            IdentityTokenPurpose.ResetPassword,
            tokenHash,
            clock.UtcNow.AddMinutes(15),
            clock.UtcNow);

        db.IdentityTokens.Add(resetToken);
        await db.SaveChangesAsync(ct);

        await emailSender.SendPasswordResetEmailAsync(user.Email, user.FullName, rawToken, ct);

        return genericResponse;
    }

    [HttpPost("reset-password"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { message = "Mã token không được để trống." });

        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.Token.Trim().ToLowerInvariant()));

        var tokenEntity = await db.IdentityTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash && x.Purpose == IdentityTokenPurpose.ResetPassword, ct);

        if (tokenEntity == null || !tokenEntity.IsValid(clock.UtcNow))
            return BadRequest(new { message = "Mã đặt lại mật khẩu không hợp lệ hoặc đã hết hạn." });

        var user = tokenEntity.User!;
        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        tokenEntity.Consume(clock.UtcNow);

        // Invalidate all active sessions on password reset
        var sessions = await db.RefreshSessions
            .Where(x => x.UserId == user.Id && x.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var s in sessions) s.Revoke(clock.UtcNow);

        await db.SaveChangesAsync(ct);

        await auditService.RecordAsync(
            user.Id,
            "Auth.ResetPassword",
            "User",
            user.Id,
            Guid.NewGuid().ToString("N"),
            new { revokedSessionsCount = sessions.Count },
            "Đặt lại mật khẩu thành công và thu hồi các phiên đăng nhập cũ",
            ct);

        return Ok(new { message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập với mật khẩu mới." });
    }

    [HttpPost("consent"), Authorize]
    public async Task<IActionResult> UpdateConsent(ConsentRequest request, CancellationToken ct)
    {
        if (request.Granted)
        {
            await consentService.GrantConsentAsync(UserId, request.Purpose, request.PolicyVersion, ct);
        }
        else
        {
            await consentService.RevokeConsentAsync(UserId, request.Purpose, request.PolicyVersion, ct);
        }

        await auditService.RecordAsync(
            UserId,
            request.Granted ? "Consent.Grant" : "Consent.Revoke",
            "ConsentRecord",
            UserId,
            Guid.NewGuid().ToString("N"),
            new { request.Purpose, request.PolicyVersion, request.Granted },
            $"Cập nhật điều khoản đồng ý: {request.Purpose} ({request.PolicyVersion})",
            ct);

        return Ok(new
        {
            message = "Cập nhật điều khoản thành công.",
            request.Purpose,
            request.PolicyVersion,
            request.Granted
        });
    }

    [HttpGet("consent"), Authorize]
    public async Task<IActionResult> GetConsent(
        [FromQuery] string purpose = "AiModelProcessing",
        [FromQuery] string policyVersion = "v1.0",
        CancellationToken ct = default)
    {
        var hasConsent = await consentService.HasValidConsentAsync(UserId, purpose, policyVersion, ct);
        return Ok(new { purpose, policyVersion, hasConsent });
    }

    [HttpGet("me"), Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking()
            .Include(u => u.CreditAccount)
            .SingleAsync(x => x.Id == UserId, ct);

        var trial = user.CreditAccount?.TrialBalance ?? user.FreeTrialRemaining;
        var purchased = user.CreditAccount?.PurchasedBalance ?? user.PurchasedRemaining;

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FullName,
            role = Roles.Name(user.Role),
            accountStatus = user.AccountStatus.ToString(),
            emailVerified = user.EmailVerifiedAtUtc.HasValue,
            freeTrialRemaining = trial,
            purchasedRemaining = purchased,
            totalBalance = trial + purchased,
            user.TotalUsed
        });
    }

    private void SetRefreshTokenCookie(string token, DateTime expires)
    {
        Response.Cookies.Append("refreshToken", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            Expires = expires
        });
    }

    private void DeleteRefreshTokenCookie()
    {
        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            Path = "/api/auth"
        });
    }
}
