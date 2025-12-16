using System.Threading.Tasks;

namespace CloudNotes.Desktop.Services;

/// <summary>
/// Авторизация на клиенте и доступ к токенам.
/// </summary>
public interface IAuthService
{
    Task<bool> RegisterAsync(string userName, string email, string password);

    Task<bool> LoginAsync(string email, string password);

    Task LogoutAsync();

    /// <summary>
    /// Есть ли сейчас валидная сессия (refresh токен).
    /// </summary>
    Task<bool> IsLoggedInAsync();

    /// <summary>
    /// Получить актуальный access токен (с попыткой refresh при необходимости).
    /// </summary>
    Task<string?> GetAccessTokenAsync();
}
