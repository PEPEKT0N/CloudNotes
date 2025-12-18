using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CloudNotes.Desktop.Services;

namespace CloudNotes.Desktop.Api;

public class AuthHeaderHandler : DelegatingHandler
{
    private readonly IAuthService _authService;

    public AuthHeaderHandler(IAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
    }

    // Автоматически добавляет Authorization Bearer header в каждый HTTP запрос
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Получаем access токен
        var accessToken = await _authService.GetAccessTokenAsync();

        // Добавляем Authorization header, если токен есть и его еще нет в запросе
        if (!string.IsNullOrEmpty(accessToken) && !request.Headers.Contains("Authorization"))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

