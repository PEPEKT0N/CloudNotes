namespace CloudNotes.Desktop.Api.DTOs;

/// <summary>
/// DTO для входа пользователя.
/// </summary>
public class LoginDto
{
    /// <summary>
    /// Email пользователя.
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// Пароль.
    /// </summary>
    public string Password { get; set; } = null!;
}

