using CloudNotes.Api.Controllers;
using CloudNotes.Api.DTOs.Auth;
using CloudNotes.Api.Models;
using CloudNotes.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace CloudNotes.Api.Tests;

/// <summary>
/// Тесты для AuthController.
/// </summary>
public class AuthControllerTests
{
    private readonly Mock<UserManager<User>> _userManagerMock;
    private readonly Mock<SignInManager<User>> _signInManagerMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        // Mock UserManager
        var userStore = new Mock<IUserStore<User>>();
        _userManagerMock = new Mock<UserManager<User>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        // Mock SignInManager
        _signInManagerMock = new Mock<SignInManager<User>>(
            _userManagerMock.Object,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<User>>().Object,
            null!, null!, null!, null!);

        // Mock TokenService
        _tokenServiceMock = new Mock<ITokenService>();

        // Mock Logger
        _loggerMock = new Mock<ILogger<AuthController>>();

        _controller = new AuthController(
            _userManagerMock.Object,
            _signInManagerMock.Object,
            _tokenServiceMock.Object,
            _loggerMock.Object);
    }

    #region Register Tests

    [Fact]
    public async Task Register_WithValidData_ReturnsCreatedWithTokens()
    {
        // Arrange
        var dto = new RegisterDto
        {
            UserName = "testuser",
            Email = "test@example.com",
            Password = "Password123"
        };

        var expectedTokens = new TokenResponseDto
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(dto.Email))
            .ReturnsAsync((User?)null);

        _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<User>(), dto.Password))
            .ReturnsAsync(IdentityResult.Success);

        _tokenServiceMock.Setup(x => x.GenerateTokensAsync(It.IsAny<User>()))
            .ReturnsAsync(expectedTokens);

        // Act
        var result = await _controller.Register(dto);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var tokens = Assert.IsType<TokenResponseDto>(createdResult.Value);
        Assert.Equal(expectedTokens.AccessToken, tokens.AccessToken);
        Assert.Equal(expectedTokens.RefreshToken, tokens.RefreshToken);
    }

    [Fact]
    public async Task Register_WithExistingEmail_ReturnsBadRequest()
    {
        // Arrange
        var dto = new RegisterDto
        {
            UserName = "testuser",
            Email = "existing@example.com",
            Password = "Password123"
        };

        var existingUser = new User { Email = dto.Email };

        _userManagerMock.Setup(x => x.FindByEmailAsync(dto.Email))
            .ReturnsAsync(existingUser);

        // Act
        var result = await _controller.Register(dto);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Register_WithInvalidPassword_ReturnsBadRequest()
    {
        // Arrange
        var dto = new RegisterDto
        {
            UserName = "testuser",
            Email = "test@example.com",
            Password = "weak"
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(dto.Email))
            .ReturnsAsync((User?)null);

        _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<User>(), dto.Password))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Password too weak" }));

        // Act
        var result = await _controller.Register(dto);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithTokens()
    {
        // Arrange
        var dto = new LoginDto
        {
            Email = "test@example.com",
            Password = "Password123"
        };

        var user = new User { Id = "user-id", Email = dto.Email, UserName = "testuser" };

        var expectedTokens = new TokenResponseDto
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(dto.Email))
            .ReturnsAsync(user);

        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, dto.Password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        _tokenServiceMock.Setup(x => x.GenerateTokensAsync(user))
            .ReturnsAsync(expectedTokens);

        // Act
        var result = await _controller.Login(dto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var tokens = Assert.IsType<TokenResponseDto>(okResult.Value);
        Assert.Equal(expectedTokens.AccessToken, tokens.AccessToken);
    }

    [Fact]
    public async Task Login_WithNonExistentEmail_ReturnsUnauthorized()
    {
        // Arrange
        var dto = new LoginDto
        {
            Email = "nonexistent@example.com",
            Password = "Password123"
        };

        _userManagerMock.Setup(x => x.FindByEmailAsync(dto.Email))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _controller.Login(dto);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        var dto = new LoginDto
        {
            Email = "test@example.com",
            Password = "WrongPassword"
        };

        var user = new User { Id = "user-id", Email = dto.Email };

        _userManagerMock.Setup(x => x.FindByEmailAsync(dto.Email))
            .ReturnsAsync(user);

        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, dto.Password, false))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        // Act
        var result = await _controller.Login(dto);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    #endregion

    #region Refresh Tests

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsOkWithNewTokens()
    {
        // Arrange
        var dto = new RefreshTokenDto { RefreshToken = "valid-refresh-token" };

        var expectedTokens = new TokenResponseDto
        {
            AccessToken = "new-access-token",
            RefreshToken = "new-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        _tokenServiceMock.Setup(x => x.RefreshTokensAsync(dto.RefreshToken))
            .ReturnsAsync(expectedTokens);

        // Act
        var result = await _controller.Refresh(dto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var tokens = Assert.IsType<TokenResponseDto>(okResult.Value);
        Assert.Equal(expectedTokens.AccessToken, tokens.AccessToken);
        Assert.Equal(expectedTokens.RefreshToken, tokens.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var dto = new RefreshTokenDto { RefreshToken = "invalid-token" };

        _tokenServiceMock.Setup(x => x.RefreshTokensAsync(dto.RefreshToken))
            .ReturnsAsync((TokenResponseDto?)null);

        // Act
        var result = await _controller.Refresh(dto);

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task Logout_WithValidToken_ReturnsNoContent()
    {
        // Arrange
        var dto = new RefreshTokenDto { RefreshToken = "valid-refresh-token" };

        _tokenServiceMock.Setup(x => x.RevokeTokenAsync(dto.RefreshToken))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Logout(dto);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Logout_WithInvalidToken_ReturnsBadRequest()
    {
        // Arrange
        var dto = new RefreshTokenDto { RefreshToken = "invalid-token" };

        _tokenServiceMock.Setup(x => x.RevokeTokenAsync(dto.RefreshToken))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.Logout(dto);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    #endregion
}

