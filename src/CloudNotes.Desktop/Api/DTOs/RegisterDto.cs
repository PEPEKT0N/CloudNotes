namespace CloudNotes.Desktop.Api.DTOs;

/// <summary>
/// DTO для регистрации нового пользователя.
/// </summary>
public class RegisterDto
{
    /// <summary>
    /// Имя пользователя.
    /// </summary>
    public string UserName { get; set; } = null!;

    /// <summary>
    /// Email пользователя.
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// Пароль.
    /// </summary>
    public string Password { get; set; } = null!;
}

