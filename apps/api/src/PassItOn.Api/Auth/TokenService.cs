using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PassItOn.Api.Domain.Entities;
using PassItOn.Api.Domain.Enums;

namespace PassItOn.Api.Auth;

public sealed class TokenService(IOptions<JwtOptions> opts)
{
    private readonly JwtOptions _opts = opts.Value;

    /// <summary>Creates a signed JWT access token for the given user.</summary>
    public string CreateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = BuildClaims(user);

        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_opts.AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Creates a cryptographically random refresh token and returns its raw value and SHA-256 hash.</summary>
    public (string Raw, string Hash) CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var raw = Convert.ToBase64String(bytes);
        var hash = HashRefreshToken(raw);
        return (raw, hash);
    }

    /// <summary>Hashes a raw refresh token for safe storage.</summary>
    public static string HashRefreshToken(string raw)
    {
        var sha256 = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToBase64String(sha256);
    }

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_opts.RefreshTokenDays);
    public int AccessTokenSeconds => _opts.AccessTokenMinutes * 60;

    private static IEnumerable<Claim> BuildClaims(User user) =>
    [
        new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new(JwtRegisteredClaimNames.Email, user.Email),
        new(ClaimTypes.Role, user.Role.ToString()),
        new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
    ];
}
