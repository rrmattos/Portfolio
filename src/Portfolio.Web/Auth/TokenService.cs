using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Portfolio.Web.Data;

namespace Portfolio.Web.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Portfolio";
    public string Audience { get; set; } = "Portfolio.Admin";
    /// <summary>HMAC-SHA256 signing key. Must have at least 32 characters.</summary>
    public string Key { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 120;

    public SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(Key));

    public TokenValidationParameters ValidationParameters => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = SigningKey,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = ClaimTypes.Name,
        RoleClaimType = ClaimTypes.Role,
    };
}

public record LoginResult(bool Success, string? Token = null, DateTime? ExpiresAt = null, string? Error = null);

/// <summary>Validates admin credentials and issues / validates JWTs.</summary>
public class TokenService(
    IDbContextFactory<AppDbContext> dbFactory,
    IOptions<JwtOptions> jwtOptions,
    IPasswordHasher<AdminUser> hasher,
    IMemoryCache cache)
{
    public const string AdminRole = "Admin";
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);

    private readonly JwtOptions _jwt = jwtOptions.Value;
    private readonly JwtSecurityTokenHandler _handler = new() { MapInboundClaims = false };

    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        email = (email ?? string.Empty).Trim().ToLowerInvariant();
        var lockKey = $"login-fail:{email}";

        if (cache.TryGetValue(lockKey, out int failures) && failures >= MaxFailedAttempts)
            return new(false, Error: "Muitas tentativas. Tente novamente em alguns minutos.");

        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.AdminUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email);

        var valid = user is not null &&
                    hasher.VerifyHashedPassword(user, user.PasswordHash, password ?? string.Empty)
                        != PasswordVerificationResult.Failed;

        if (!valid)
        {
            cache.Set(lockKey, failures + 1, LockoutWindow);
            return new(false, Error: "E-mail ou senha inválidos.");
        }

        cache.Remove(lockKey);
        var (token, expires) = CreateToken(user!);
        return new(true, token, expires);
    }

    public (string Token, DateTime ExpiresAt) CreateToken(AdminUser user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_jwt.ExpirationMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, AdminRole),
        };

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: new SigningCredentials(_jwt.SigningKey, SecurityAlgorithms.HmacSha256));

        return (_handler.WriteToken(token), expires);
    }

    /// <summary>Returns the principal for a valid token, or null if invalid/expired.</summary>
    public ClaimsPrincipal? ValidateToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            return _handler.ValidateToken(token, _jwt.ValidationParameters, out _);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }
}
