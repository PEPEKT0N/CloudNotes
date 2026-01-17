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

        return await base.SendAsync(request, cancellationToken);
    }
}

