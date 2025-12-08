using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CloudNotes.Api.Data;
using CloudNotes.Api.Models;
using CloudNotes.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace CloudNotes.Api.Tests;

/// <summary>
/// Тесты для TokenService.
/// </summary>
public class TokenServiceTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly TokenService _tokenService;
    private readonly User _testUser;

    public TokenServiceTests()
    {
        // InMemory Database
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApiDbContext(options);

        // Mock Configuration
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(c => c["Jwt:Secret"])
            .Returns("CloudNotes-Test-Secret-Key-Min-32-Characters!!");
        configuration.Setup(c => c["Jwt:Issuer"])
            .Returns("CloudNotes.Api.Test");
        configuration.Setup(c => c["Jwt:Audience"])
            .Returns("CloudNotes.Client.Test");
        configuration.Setup(c => c.GetSection("Jwt:AccessTokenExpirationMinutes").Value)
            .Returns("60");
        configuration.Setup(c => c.GetSection("Jwt:RefreshTokenExpirationDays").Value)
            .Returns("7");

        // Mock Logger
        var logger = new Mock<ILogger<TokenService>>();

        _tokenService = new TokenService(_context, configuration.Object, logger.Object);

        // Test User - сохраняем в БД для корректной работы Include в RefreshTokensAsync
        _testUser = new User
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "testuser",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.Add(_testUser);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GenerateTokensAsync Tests

    [Fact]
    public async Task GenerateTokensAsync_ShouldReturnValidTokens()
    {
        // Act
        var result = await _tokenService.GenerateTokensAsync(_testUser);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task GenerateTokensAsync_AccessToken_ShouldContainCorrectClaims()
    {
        // Act
        var result = await _tokenService.GenerateTokensAsync(_testUser);

        // Parse JWT
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);

        // Assert
        Assert.Equal(_testUser.Id, token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(_testUser.Email, token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("CloudNotes.Api.Test", token.Issuer);
        Assert.Contains("CloudNotes.Client.Test", token.Audiences);
    }

    [Fact]
    public async Task GenerateTokensAsync_ShouldSaveRefreshTokenToDatabase()
    {
        // Act
        var result = await _tokenService.GenerateTokensAsync(_testUser);

        // Assert
        var savedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == result.RefreshToken);

        Assert.NotNull(savedToken);
        Assert.Equal(_testUser.Id, savedToken.UserId);
        Assert.True(savedToken.IsActive);
    }

    #endregion

    #region RefreshTokensAsync Tests

    [Fact]
    public async Task RefreshTokensAsync_WithValidToken_ShouldReturnNewTokens()
    {
        // Arrange
        var initialTokens = await _tokenService.GenerateTokensAsync(_testUser);

        // Act
        var result = await _tokenService.RefreshTokensAsync(initialTokens.RefreshToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(initialTokens.AccessToken, result.AccessToken);
        Assert.NotEqual(initialTokens.RefreshToken, result.RefreshToken);
    }

    [Fact]
    public async Task RefreshTokensAsync_ShouldRevokeOldToken()
    {
        // Arrange
        var initialTokens = await _tokenService.GenerateTokensAsync(_testUser);

        // Act
        await _tokenService.RefreshTokensAsync(initialTokens.RefreshToken);

        // Assert
        var oldToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == initialTokens.RefreshToken);

        Assert.NotNull(oldToken);
        Assert.NotNull(oldToken.RevokedAt);
        Assert.False(oldToken.IsActive);
    }

    [Fact]
    public async Task RefreshTokensAsync_WithInvalidToken_ShouldReturnNull()
    {
        // Act
        var result = await _tokenService.RefreshTokensAsync("invalid-token");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokensAsync_WithRevokedToken_ShouldReturnNull()
    {
        // Arrange
        var initialTokens = await _tokenService.GenerateTokensAsync(_testUser);
        await _tokenService.RevokeTokenAsync(initialTokens.RefreshToken);

        // Act
        var result = await _tokenService.RefreshTokensAsync(initialTokens.RefreshToken);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region RevokeTokenAsync Tests

    [Fact]
    public async Task RevokeTokenAsync_WithValidToken_ShouldReturnTrue()
    {
        // Arrange
        var tokens = await _tokenService.GenerateTokensAsync(_testUser);

        // Act
        var result = await _tokenService.RevokeTokenAsync(tokens.RefreshToken);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task RevokeTokenAsync_ShouldMarkTokenAsRevoked()
    {
        // Arrange
        var tokens = await _tokenService.GenerateTokensAsync(_testUser);

        // Act
        await _tokenService.RevokeTokenAsync(tokens.RefreshToken);

        // Assert
        var revokedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == tokens.RefreshToken);

        Assert.NotNull(revokedToken);
        Assert.NotNull(revokedToken.RevokedAt);
        Assert.False(revokedToken.IsActive);
    }

    [Fact]
    public async Task RevokeTokenAsync_WithInvalidToken_ShouldReturnFalse()
    {
        // Act
        var result = await _tokenService.RevokeTokenAsync("invalid-token");

        // Assert
        Assert.False(result);
    }

    #endregion

    #region RevokeAllUserTokensAsync Tests

    [Fact]
    public async Task RevokeAllUserTokensAsync_ShouldRevokeAllActiveTokens()
    {
        // Arrange
        await _tokenService.GenerateTokensAsync(_testUser);
        await _tokenService.GenerateTokensAsync(_testUser);
        await _tokenService.GenerateTokensAsync(_testUser);

        // Act
        var revokedCount = await _tokenService.RevokeAllUserTokensAsync(_testUser.Id);

        // Assert
        Assert.Equal(3, revokedCount);

        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == _testUser.Id && rt.RevokedAt == null)
            .CountAsync();
        Assert.Equal(0, activeTokens);
    }

    [Fact]
    public async Task RevokeAllUserTokensAsync_WithNoTokens_ShouldReturnZero()
    {
        // Act
        var revokedCount = await _tokenService.RevokeAllUserTokensAsync(_testUser.Id);

        // Assert
        Assert.Equal(0, revokedCount);
    }

    #endregion
}

