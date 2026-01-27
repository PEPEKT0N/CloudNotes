using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CloudNotes.Desktop.Services;

namespace CloudNotes.Desktop.Api;

public class AuthHeaderHandler : DelegatingHandler
{
    private readonly Func<IAuthService> _authServiceFactory;

    public AuthHeaderHandler(Func<IAuthService> authServiceFactory)
    {
        _authServiceFactory = authServiceFactory ?? throw new ArgumentNullException(nameof(authServiceFactory));
    }

    // Автоматически добавляет Authorization Bearer header в каждый HTTP запрос
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Пропускаем auth endpoints - они не требуют токена
        var path = request.RequestUri?.AbsolutePath ?? "";
        if (path.Contains("/auth/login") || path.Contains("/auth/register") || path.Contains("/auth/refresh"))
        {
            Console.WriteLine($"[AuthHandler] Skipping auth endpoint: {path}");
            return await base.SendAsync(request, cancellationToken);
        }

        // Получаем AuthService лениво
        var authService = _authServiceFactory();

        // Получаем access токен
        var accessToken = await authService.GetAccessTokenAsync();

        Console.WriteLine($"[AuthHandler] Request to {request.RequestUri}");
        Console.WriteLine($"[AuthHandler] AccessToken: {(string.IsNullOrEmpty(accessToken) ? "NULL/EMPTY" : $"present ({accessToken.Length} chars)")}");

        // Добавляем Authorization header, если токен есть и его еще нет в запросе
        if (!string.IsNullOrEmpty(accessToken) && !request.Headers.Contains("Authorization"))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            Console.WriteLine("[AuthHandler] Authorization header ADDED");
        }
        else if (string.IsNullOrEmpty(accessToken))
        {
            Console.WriteLine("[AuthHandler] WARNING: No token available, request will be unauthenticated!");
        }

        // Отправляем запрос
        var response = await base.SendAsync(request, cancellationToken);

        // Если получили 401, пытаемся обновить токен и повторить запрос
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            Console.WriteLine("[AuthHandler] Got 401 Unauthorized, attempting force token refresh...");

            // Принудительно обновляем токен (токен уже истек, поэтому нужно обновить)
            var newAccessToken = await authService.ForceRefreshTokenAsync();

            if (!string.IsNullOrEmpty(newAccessToken))
            {
                Console.WriteLine($"[AuthHandler] Token refreshed, retrying request with new token ({newAccessToken.Length} chars)");

                // Создаем новый запрос (старый уже использован)
                var retryRequest = CloneRequest(request);
                retryRequest.Headers.Authorization = null; // Очищаем старый заголовок
                retryRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newAccessToken);

                // Повторяем запрос
                response = await base.SendAsync(retryRequest, cancellationToken);

                // Если снова 401, значит refresh token тоже недействителен
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Console.WriteLine("[AuthHandler] Still 401 after refresh - refresh token invalid, user needs to re-login");
                }
            }
            else
            {
                Console.WriteLine("[AuthHandler] Token refresh failed, user needs to re-login");
            }
        }

        return response;
    }

    private HttpRequestMessage CloneRequest(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version
        };

        // Копируем Content если есть
        if (original.Content != null)
        {
            clone.Content = original.Content;
        }

        // Копируем заголовки (кроме Authorization, который мы обновим)
        foreach (var header in original.Headers)
        {
            if (header.Key != "Authorization")
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}

