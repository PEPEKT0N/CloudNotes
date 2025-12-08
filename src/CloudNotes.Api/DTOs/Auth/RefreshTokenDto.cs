using System.ComponentModel.DataAnnotations;

namespace CloudNotes.Api.DTOs.Auth;

/// <summary>
/// DTO для обновления токена.
/// </summary>
public class RefreshTokenDto
{
    /// <summary>
    /// Refresh токен.
    /// </summary>
    [Required(ErrorMessage = "Refresh токен обязателен")]
    public string RefreshToken { get; set; } = null!;
}

