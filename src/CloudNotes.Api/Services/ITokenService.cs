using CloudNotes.Api.DTOs.Auth;
using CloudNotes.Api.Models;

namespace CloudNotes.Api.Services;

/// <summary>
/// Сервис для работы с JWT и Refresh токенами.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Генерирует пару токенов (access + refresh) для пользователя.
    /// </summary>
    /// <param name="user">Пользователь.</param>
    /// <returns>DTO с токенами.</returns>
    Task<TokenResponseDto> GenerateTokensAsync(User user);

    /// <summary>
    /// Обновляет токены по refresh токену.
    /// </summary>
    /// <param name="refreshToken">Текущий refresh токен.</param>
    /// <returns>Новые токены или null, если refresh токен невалиден.</returns>
    Task<TokenResponseDto?> RefreshTokensAsync(string refreshToken);

    /// <summary>
    /// Отзывает refresh токен (logout).
    /// </summary>
    /// <param name="refreshToken">Refresh токен для отзыва.</param>
    /// <returns>True, если токен успешно отозван.</returns>
    Task<bool> RevokeTokenAsync(string refreshToken);

    /// <summary>
    /// Отзывает все refresh токены пользователя.
    /// </summary>
    /// <param name="userId">ID пользователя.</param>
    /// <returns>Количество отозванных токенов.</returns>
    Task<int> RevokeAllUserTokensAsync(string userId);
}

