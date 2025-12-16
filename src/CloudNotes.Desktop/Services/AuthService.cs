using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CloudNotes.Desktop.Api;
using CloudNotes.Desktop.Api.DTOs;

namespace CloudNotes.Desktop.Services;

public class AuthService : IAuthService
{
    private const string TokensFileName = "tokens.json";

    private readonly ICloudNotesApi _api;

    public AuthService(ICloudNotesApi api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    public async Task<bool> RegisterAsync(string userName, string email, string password)
    {
        var dto = new RegisterDto
        {
            UserName = userName,
            Email = email,
            Password = password
        };

        var tokenResponse = await _api.RegisterAsync(dto);
        await SaveTokensAsync(tokenResponse);

        return true;
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        var dto = new LoginDto
        {
            Email = email,
            Password = password
        };

        var tokenResponse = await _api.LoginAsync(dto);
        await SaveTokensAsync(tokenResponse);

        return true;
    }

    public async Task LogoutAsync()
    {
        var tokens = await LoadTokensAsync();
        if (tokens != null)
        {
            try
            {
                await _api.LogoutAsync(new RefreshTokenDto
                {
                    RefreshToken = tokens.RefreshToken
                });
            }
            catch
            {
                // Игнорируем сетевые ошибки при logout: главное — удалить локальные токены.
            }
        }

        DeleteTokensFile();
    }

    public async Task<bool> IsLoggedInAsync()
    {
        var tokens = await LoadTokensAsync();
        if (tokens is null)
        {
            return false;
        }

        // Считаем пользователя авторизованным, если есть refresh токен.
        // Дополнительная проверка срока действия выполняется при получении access токена.
        return !string.IsNullOrWhiteSpace(tokens.RefreshToken);
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        var tokens = await LoadTokensAsync();
        return tokens?.AccessToken;
    }

    private static string GetTokensFilePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CloudNotes"
        );

        Directory.CreateDirectory(folder);

        return Path.Combine(folder, TokensFileName);
    }

    private static async Task<AuthTokens?> LoadTokensAsync()
    {
        var path = GetTokensFilePath();
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<AuthTokens>(stream);
    }

    private static async Task SaveTokensAsync(TokenResponseDto dto)
    {
        var tokens = new AuthTokens
        {
            AccessToken = dto.AccessToken,
            RefreshToken = dto.RefreshToken,
            ExpiresAt = dto.ExpiresAt
        };

        var path = GetTokensFilePath();

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, tokens, options);
    }

    private static void DeleteTokensFile()
    {
        var path = GetTokensFilePath();
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private async Task<AuthTokens?> TryRefreshTokensAsync(AuthTokens currentTokens)
    {
        if (string.IsNullOrWhiteSpace(currentTokens.RefreshToken))
        {
            DeleteTokensFile();
            return null;
        }

        try
        {
            var response = await _api.RefreshAsync(new RefreshTokenDto
            {
                RefreshToken = currentTokens.RefreshToken
            });

            await SaveTokensAsync(response);

            return new AuthTokens
            {
                AccessToken = response.AccessToken,
                RefreshToken = response.RefreshToken,
                ExpiresAt = response.ExpiresAt
            };
        }
        catch
        {
            DeleteTokensFile();
            return null;
        }
    }

    private sealed class AuthTokens
    {
        public string AccessToken { get; set; } = string.Empty;

        public string RefreshToken { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
    }
}


