using System.ComponentModel.DataAnnotations;

namespace CloudNotes.Api.DTOs.Auth;

/// <summary>
/// DTO для входа пользователя.
/// </summary>
public class LoginDto
{
    /// <summary>
    /// Email пользователя.
    /// </summary>
    [Required(ErrorMessage = "Email обязателен")]
    [EmailAddress(ErrorMessage = "Некорректный формат email")]
    public string Email { get; set; } = null!;

    /// <summary>
    /// Пароль.
    /// </summary>
    [Required(ErrorMessage = "Пароль обязателен")]
    public string Password { get; set; } = null!;
}

