using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CloudNotes.Api.Data;
using CloudNotes.Api.DTOs.Auth;
using CloudNotes.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CloudNotes.Api.Services;

/// <summary>
/// Реализация сервиса для работы с JWT и Refresh токенами.
/// </summary>
public class TokenService : ITokenService
{
    private readonly ApiDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenService> _logger;

    public TokenService(
        ApiDbContext context,
        IConfiguration configuration,
        ILogger<TokenService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TokenResponseDto> GenerateTokensAsync(User user)
    {
        var accessToken = GenerateAccessToken(user);
        var refreshToken = await GenerateRefreshTokenAsync(user);

        _logger.LogInformation("Токены сгенерированы для пользователя {UserId}", user.Id);

        return new TokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(GetAccessTokenExpirationMinutes()),
            UserName = user.UserName ?? string.Empty
        };
    }

    /// <inheritdoc />
    public async Task<TokenResponseDto?> RefreshTokensAsync(string refreshToken)
    {
        var storedToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (storedToken == null)
        {
            _logger.LogWarning("Попытка обновления с несуществующим refresh токеном");
            return null;
        }

        if (!storedToken.IsActive)
        {
            _logger.LogWarning(
                "Попытка использования неактивного refresh токена для пользователя {UserId}",
                storedToken.UserId);
            return null;
        }

        // Отзываем старый токен и создаём новый (ротация токенов)
        storedToken.RevokedAt = DateTime.UtcNow;
        var newRefreshToken = await GenerateRefreshTokenAsync(storedToken.User);
        storedToken.ReplacedByToken = newRefreshToken.Token;

        await _context.SaveChangesAsync();

        var accessToken = GenerateAccessToken(storedToken.User);

        _logger.LogInformation(
            "Токены обновлены для пользователя {UserId}",
            storedToken.UserId);

        return new TokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken.Token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(GetAccessTokenExpirationMinutes()),
            UserName = storedToken.User.UserName ?? string.Empty
        };
    }

    /// <inheritdoc />
    public async Task<bool> RevokeTokenAsync(string refreshToken)
    {
        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

        if (storedToken == null || !storedToken.IsActive)
        {
            return false;
        }

        storedToken.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Refresh токен отозван для пользователя {UserId}",
            storedToken.UserId);

        return true;
    }

    /// <inheritdoc />
    public async Task<int> RevokeAllUserTokensAsync(string userId)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Отозвано {Count} refresh токенов для пользователя {UserId}",
            activeTokens.Count, userId);

        return activeTokens.Count;
    }

    /// <summary>
    /// Генерирует JWT access токен.
    /// </summary>
    private string GenerateAccessToken(User user)
    {
        var secret = _configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT Secret не настроен");
        var issuer = _configuration["Jwt:Issuer"] ?? "CloudNotes.Api";
        var audience = _configuration["Jwt:Audience"] ?? "CloudNotes.Client";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(GetAccessTokenExpirationMinutes()),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Генерирует и сохраняет refresh токен.
    /// </summary>
    private async Task<RefreshToken> GenerateRefreshTokenAsync(User user)
    {
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = GenerateRandomToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenExpirationDays()),
            CreatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        return refreshToken;
    }

    /// <summary>
    /// Генерирует криптографически безопасную случайную строку.
    /// </summary>
    private static string GenerateRandomToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private int GetAccessTokenExpirationMinutes()
    {
        return _configuration.GetValue("Jwt:AccessTokenExpirationMinutes", 60);
    }

    private int GetRefreshTokenExpirationDays()
    {
        return _configuration.GetValue("Jwt:RefreshTokenExpirationDays", 7);
    }
}

