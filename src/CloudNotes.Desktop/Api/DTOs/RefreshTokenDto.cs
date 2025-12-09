namespace CloudNotes.Desktop.Api.DTOs;

/// <summary>
/// DTO для обновления токена.
/// </summary>
public class RefreshTokenDto
{
    /// <summary>
    /// Refresh токен.
    /// </summary>
    public string RefreshToken { get; set; } = null!;
}

