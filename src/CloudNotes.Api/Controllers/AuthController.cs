using CloudNotes.Api.DTOs.Auth;
using CloudNotes.Api.Models;
using CloudNotes.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CloudNotes.Api.Controllers;

/// <summary>
/// Контроллер аутентификации.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ITokenService tokenService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    /// <summary>
    /// Регистрация нового пользователя.
    /// </summary>
    /// <param name="dto">Данные для регистрации.</param>
    /// <returns>Токены доступа.</returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
        {
            return BadRequest(new { error = "Пользователь с таким email уже существует" });
        }

        var user = new User
        {
            UserName = dto.UserName,
            Email = dto.Email,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description);
            return BadRequest(new { errors });
        }

        _logger.LogInformation("Пользователь {Email} успешно зарегистрирован", dto.Email);

        var tokens = await _tokenService.GenerateTokensAsync(user);

        return CreatedAtAction(nameof(Register), tokens);
    }

    /// <summary>
    /// Вход пользователя.
    /// </summary>
    /// <param name="dto">Данные для входа.</param>
    /// <returns>Токены доступа.</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
        {
            return Unauthorized(new { error = "Неверный email или пароль" });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            return Unauthorized(new { error = "Неверный email или пароль" });
        }

        _logger.LogInformation("Пользователь {Email} успешно вошёл", dto.Email);

        var tokens = await _tokenService.GenerateTokensAsync(user);

        return Ok(tokens);
    }

    /// <summary>
    /// Обновление access токена.
    /// </summary>
    /// <param name="dto">Refresh токен.</param>
    /// <returns>Новые токены.</returns>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto)
    {
        var tokens = await _tokenService.RefreshTokensAsync(dto.RefreshToken);

        if (tokens == null)
        {
            return Unauthorized(new { error = "Недействительный или истёкший refresh токен" });
        }

        _logger.LogInformation("Токены успешно обновлены");

        return Ok(tokens);
    }

    /// <summary>
    /// Выход (инвалидация refresh токена).
    /// </summary>
    /// <param name="dto">Refresh токен для инвалидации.</param>
    /// <returns>204 No Content.</returns>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenDto dto)
    {
        var revoked = await _tokenService.RevokeTokenAsync(dto.RefreshToken);

        if (!revoked)
        {
            return BadRequest(new { error = "Токен не найден или уже отозван" });
        }

        _logger.LogInformation("Пользователь вышел из системы");

        return NoContent();
    }
}
