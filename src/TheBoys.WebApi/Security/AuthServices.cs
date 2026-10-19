using System.IdentityModel.Tokens.Jwt;
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
}

public static class Roles
{
    public const string Customer = "cus";
    public const string Staff = "staff";
    public const string Admin = "admin";
    public const string Management = "staff,admin";
    public static string Name(UserRole role) => role switch
    { UserRole.Customer => Customer, UserRole.Staff => Staff, UserRole.Admin => Admin, _ => "disabled" };
}

public sealed class TokenService(JwtOptions options)
{
    public object Create(User user)
    {
        var expires = DateTime.UtcNow.AddMinutes(options.AccessTokenMinutes);
        var token = new JwtSecurityToken(options.Issuer, options.Audience,
            [new("sub", user.Id.ToString()), new("role", Roles.Name(user.Role)), new("jti", Guid.NewGuid().ToString())],
            expires: expires, signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret)), SecurityAlgorithms.HmacSha256));
        return new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc = expires,
            user = new { user.Id, user.Email, user.FullName, role = Roles.Name(user.Role), user.FreeTrialRemaining, user.PurchasedRemaining } };
    }
}
