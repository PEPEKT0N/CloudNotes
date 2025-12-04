namespace CloudNotes.Api.Models;

/// <summary>
/// Refresh-токен для обновления JWT access-токена.
/// </summary>
public class RefreshToken
{
    /// <summary>
    /// Уникальный идентификатор токена.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Идентификатор пользователя-владельца токена.
    /// </summary>
    public string UserId { get; set; } = null!;

    /// <summary>
    /// Пользователь-владелец токена.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Значение токена.
    /// </summary>
    public string Token { get; set; } = null!;

    /// <summary>
    /// Дата истечения срока действия токена.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Дата создания токена.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Дата отзыва токена (null если токен активен).
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Токен, которым был заменён данный токен (при ротации).
    /// </summary>
    public string? ReplacedByToken { get; set; }

    /// <summary>
    /// Проверяет, активен ли токен.
    /// </summary>
    public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;
}

