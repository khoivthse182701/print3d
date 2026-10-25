using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TheBoys.Domain.Entities;
using TheBoys.Domain.Enums;

namespace TheBoys.WebApi.Security;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "TheBoys";
    public string Audience { get; set; } = "TheBoys.WebApi";
    public string Secret { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 7;
}

public static class Roles
{
    public const string Customer = "cus";
    public const string Staff = "staff";
    public const string Admin = "admin";
    public const string Management = "staff,admin";
    public static string Name(UserRole role) => role switch
    {
        UserRole.Customer => Customer,
        UserRole.Staff => Staff,
        UserRole.Admin => Admin,
        _ => "disabled"
    };
}

public sealed class TokenService(JwtOptions options)
{
    public (string accessToken, DateTime expiresAtUtc) CreateAccessToken(User user, Guid? sessionId = null)
    {
        var expires = DateTime.UtcNow.AddMinutes(options.AccessTokenMinutes);
        var claims = new List<System.Security.Claims.Claim>
        {
            new("sub", user.Id.ToString()),
            new("role", Roles.Name(user.Role)),
            new("jti", Guid.NewGuid().ToString())
        };

        if (sessionId.HasValue)
        {
            claims.Add(new("sid", sessionId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            claims,
            expires: expires,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret)),
                SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public object Create(User user) => new
    {
        accessToken = CreateAccessToken(user).accessToken,
        expiresIn = options.AccessTokenMinutes * 60
    };

    public static (string rawToken, byte[] tokenHash) GenerateSecureToken()
    {
        var rawBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToHexString(rawBytes).ToLowerInvariant();
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return (rawToken, tokenHash);
    }

    public static (string rawToken, byte[] tokenHash) GenerateOtpCode(int length = 6)
    {
        const string chars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        var rawToken = RandomNumberGenerator.GetString(chars, length);
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken.ToLowerInvariant()));
        return (rawToken, tokenHash);
    }

    public object CreateResponse(User user, string accessToken, DateTime expiresAtUtc, string? refreshToken = null)
    {
        var trial = user.CreditAccount?.TrialBalance ?? user.FreeTrialRemaining;
        var purchased = user.CreditAccount?.PurchasedBalance ?? user.PurchasedRemaining;

        return new
        {
            accessToken,
            expiresAtUtc,
            refreshToken,
            user = new
            {
                user.Id,
                user.Email,
                user.FullName,
                role = Roles.Name(user.Role),
                accountStatus = user.AccountStatus.ToString(),
                emailVerified = user.EmailVerifiedAtUtc.HasValue,
                freeTrialRemaining = trial,
                purchasedRemaining = purchased
            }
        };
    }
}

