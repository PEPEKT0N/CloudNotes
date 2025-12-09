using System.Security.Claims;

namespace CloudNotes.Api.Extensions;

/// <summary>
/// Расширения для HttpContext.
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Получает ID текущего аутентифицированного пользователя.
    /// </summary>
    /// <param name="httpContext">HTTP контекст.</param>
    /// <returns>ID пользователя или null, если пользователь не аутентифицирован.</returns>
    public static string? GetUserId(this HttpContext httpContext)
    {
        return httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    /// <summary>
    /// Получает ID текущего аутентифицированного пользователя.
    /// Выбрасывает исключение, если пользователь не аутентифицирован.
    /// </summary>
    /// <param name="httpContext">HTTP контекст.</param>
    /// <returns>ID пользователя.</returns>
    /// <exception cref="UnauthorizedAccessException">Пользователь не аутентифицирован.</exception>
    public static string GetRequiredUserId(this HttpContext httpContext)
    {
        var userId = httpContext.GetUserId();

        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("Пользователь не аутентифицирован");
        }

        return userId;
    }

    /// <summary>
    /// Получает email текущего аутентифицированного пользователя.
    /// </summary>
    /// <param name="httpContext">HTTP контекст.</param>
    /// <returns>Email пользователя или null.</returns>
    public static string? GetUserEmail(this HttpContext httpContext)
    {
        return httpContext.User.FindFirstValue(ClaimTypes.Email);
    }

    /// <summary>
    /// Проверяет, аутентифицирован ли пользователь.
    /// </summary>
    /// <param name="httpContext">HTTP контекст.</param>
    /// <returns>True, если пользователь аутентифицирован.</returns>
    public static bool IsAuthenticated(this HttpContext httpContext)
    {
        return httpContext.User.Identity?.IsAuthenticated ?? false;
    }
}

