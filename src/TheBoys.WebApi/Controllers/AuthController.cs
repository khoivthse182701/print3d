using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TheBoys.Domain.Entities;
using TheBoys.Infrastructure.Persistence;
using TheBoys.WebApi.Security;

namespace TheBoys.WebApi.Controllers;

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

[ApiController, Route("api/auth"), EnableRateLimiting("auth")]
public sealed class AuthController(ApplicationDbContext db, IPasswordHasher<User> hasher, TokenService tokens) : ApiControllerBase
{
    [HttpPost("register"), AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email, ct)) return Conflict(new { message = "Email đã được sử dụng." });
        if (string.IsNullOrWhiteSpace(request.FullName)) return BadRequest(new { message = "Tên không hợp lệ." });
        var user = new User { Email = email, FullName = request.FullName.Trim() };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return Ok(tokens.Create(user));
    }

    [HttpPost("login"), AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email, ct);
        if (user?.PasswordHash == null) return Unauthorized(new { message = "Email hoặc mật khẩu không đúng." });
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed) return Unauthorized(new { message = "Email hoặc mật khẩu không đúng." });
        if (Roles.Name(user.Role) == "disabled") return Forbid();
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        { user.PasswordHash = hasher.HashPassword(user, request.Password); await db.SaveChangesAsync(ct); }
        return Ok(tokens.Create(user));
    }

    [HttpGet("me"), Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == UserId, ct);
        return Ok(new { user.Id, user.Email, user.FullName, role = Roles.Name(user.Role), user.FreeTrialRemaining, user.PurchasedRemaining, user.TotalUsed });
    }
}
