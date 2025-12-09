using System;

namespace CloudNotes.Desktop.Api.DTOs;

/// <summary>
/// DTO с токенами для ответа от сервера.
/// </summary>
public class TokenResponseDto
{
    /// <summary>
    /// JWT Access токен.
    /// </summary>
    public string AccessToken { get; set; } = null!;

    /// <summary>
    /// Refresh токен для обновления access токена.
    /// </summary>
    public string RefreshToken { get; set; } = null!;

    /// <summary>
    /// Время истечения access токена (UTC).
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}

