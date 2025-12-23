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
        // Получаем AuthService лениво
        var authService = _authServiceFactory();

        // Получаем access токен
        var accessToken = await authService.GetAccessTokenAsync();

        // Добавляем Authorization header, если токен есть и его еще нет в запросе
        if (!string.IsNullOrEmpty(accessToken) && !request.Headers.Contains("Authorization"))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

