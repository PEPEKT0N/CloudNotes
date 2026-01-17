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
    private const string LastUserFileName = "last_user.txt";

    private readonly ICloudNotesApi _api;
    
    // Кэш токенов в памяти для избежания race condition при чтении из файла
    private static AuthTokens? _cachedTokens;
    private static readonly object _cacheLock = new();

    public AuthService(ICloudNotesApi api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    /// <summary>
    /// Получает email последнего авторизованного пользователя (сохраняется даже после logout).
    /// </summary>
    public string? GetLastLoggedInEmail()
    {
        var path = GetLastUserFilePath();
        if (File.Exists(path))
        {
            try
            {
                return File.ReadAllText(path).Trim();
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    private static void SaveLastLoggedInEmail(string email)
    {
        var path = GetLastUserFilePath();
        File.WriteAllText(path, email);
        Console.WriteLine($"[AuthService] Saved last user email: {email}");
    }

    private static string GetLastUserFilePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CloudNotes"
        );
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, LastUserFileName);
    }

    public async Task<bool> RegisterAsync(string userName, string email, string password)
    {
        try
        {
            var dto = new RegisterDto
            {
                UserName = userName,
                Email = email,
                Password = password
            };

            var tokenResponse = await _api.RegisterAsync(dto);
            await SaveTokensAsync(tokenResponse, email, userName);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Register error: {ex}");
            throw; // Пробрасываем исключение дальше для обработки в UI
        }
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        try
        {
            var dto = new LoginDto
            {
                Email = email,
                Password = password
            };

            var tokenResponse = await _api.LoginAsync(dto);
            await SaveTokensAsync(tokenResponse, email);

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Login error: {ex}");
            throw; // Пробрасываем исключение дальше для обработки в UI
        }
    }

    /// <summary>
    /// Получает email текущего авторизованного пользователя.
    /// </summary>
    public async Task<string?> GetCurrentUserEmailAsync()
    {
        var tokens = await LoadTokensAsync();
        return tokens?.Email;
    }

    /// <summary>
    /// Получает имя текущего авторизованного пользователя.
    /// </summary>
    public async Task<string?> GetCurrentUserNameAsync()
    {
        var tokens = await LoadTokensAsync();
        return tokens?.UserName;
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

        // Проверяем наличие refresh токена
        if (string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            return false;
        }

        // Проверяем срок действия токена (ExpiresAt - это время истечения access token)
        // Если токен истек давно (более 1 дня назад), считаем неавторизованным
        var now = DateTime.UtcNow;
        if (tokens.ExpiresAt < now.AddDays(-1))
        {
            // Токен истек, удаляем файл с токенами
            DeleteTokensFile();
            return false;
        }

        return true;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        Console.WriteLine("[AuthService] GetAccessTokenAsync called");
        var tokens = await LoadTokensAsync();
        if (tokens is null)
        {
            Console.WriteLine("[AuthService] No tokens found!");
            return null;
        }

        Console.WriteLine($"[AuthService] Token loaded. Email={tokens.Email}, ExpiresAt={tokens.ExpiresAt:O}, TokenLen={tokens.AccessToken?.Length ?? 0}");

        var now = DateTime.UtcNow;
        var skew = TimeSpan.FromMinutes(1);
        var needsRefresh = tokens.ExpiresAt <= now.Add(skew);

        Console.WriteLine($"[AuthService] Now(UTC)={now:O}, NeedsRefresh={needsRefresh}");

        if (needsRefresh)
        {
            Console.WriteLine("[AuthService] Token needs refresh, attempting...");
            tokens = await TryRefreshTokensAsync(tokens);
            if (tokens is null)
            {
                Console.WriteLine("[AuthService] Token refresh FAILED!");
                return null;
            }
            Console.WriteLine("[AuthService] Token refreshed successfully");
        }

        Console.WriteLine($"[AuthService] Returning token ({tokens.AccessToken?.Length ?? 0} chars)");
        return tokens.AccessToken;
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

    private async Task<AuthTokens?> LoadTokensAsync()
    {
        // Сначала проверяем кэш в памяти
        lock (_cacheLock)
        {
            if (_cachedTokens != null)
            {
                Console.WriteLine($"[AuthService] LoadTokens: FROM CACHE. Email={_cachedTokens.Email}");
                return _cachedTokens;
            }
        }

        // Если кэш пуст, загружаем из файла
        var path = GetTokensFilePath();
        Console.WriteLine($"[AuthService] LoadTokens: Cache empty, reading file: {path}");
        
        if (!File.Exists(path))
        {
            Console.WriteLine("[AuthService] LoadTokens: File does not exist");
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var tokens = await JsonSerializer.DeserializeAsync<AuthTokens>(stream);
            
            // Сохраняем в кэш
            if (tokens != null)
            {
                lock (_cacheLock)
                {
                    _cachedTokens = tokens;
                }
                Console.WriteLine($"[AuthService] LoadTokens: Loaded from file and cached. Email={tokens.Email}");
            }
            
            return tokens;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuthService] LoadTokens ERROR: {ex.Message}");
            return null;
        }
    }

    private async Task SaveTokensAsync(TokenResponseDto dto, string email, string? userName = null)
    {
        Console.WriteLine($"[AuthService] SaveTokens: Email={email}, ExpiresAt={dto.ExpiresAt:O}");
        
        var tokens = new AuthTokens
        {
            AccessToken = dto.AccessToken,
            RefreshToken = dto.RefreshToken,
            ExpiresAt = dto.ExpiresAt,
            Email = email,
            UserName = userName
        };

        // Сохраняем в кэш СРАЗУ (до записи в файл)
        lock (_cacheLock)
        {
            _cachedTokens = tokens;
        }
        Console.WriteLine($"[AuthService] SaveTokens: CACHED in memory. TokenLen={tokens.AccessToken?.Length ?? 0}");

        // Сохраняем email последнего пользователя (не удаляется при logout)
        SaveLastLoggedInEmail(email);

        var path = GetTokensFilePath();

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, tokens, options);
        
        // Ensure the file is flushed to disk
        await stream.FlushAsync();
        
        Console.WriteLine("[AuthService] SaveTokens: Written to file");
    }

    private void DeleteTokensFile()
    {
        // Очищаем кэш
        lock (_cacheLock)
        {
            _cachedTokens = null;
        }
        System.Diagnostics.Debug.WriteLine("DeleteTokensFile: Cache cleared");
        
        var path = GetTokensFilePath();
        if (File.Exists(path))
        {
            File.Delete(path);
            System.Diagnostics.Debug.WriteLine("DeleteTokensFile: File deleted");
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

            // Сохраняем токены вместе с email и userName из текущих токенов
            // SaveTokensAsync также обновит кэш
            await SaveTokensAsync(response, currentTokens.Email ?? string.Empty, currentTokens.UserName);

            // Возвращаем токены из кэша (они уже обновлены в SaveTokensAsync)
            lock (_cacheLock)
            {
                return _cachedTokens;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"TryRefreshTokensAsync: Failed to refresh: {ex.Message}");
            DeleteTokensFile();
            return null;
        }
    }

    private sealed class AuthTokens
    {
        public string AccessToken { get; set; } = string.Empty;

        public string RefreshToken { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Email пользователя (сохраняем для идентификации).
        /// </summary>
        public string? Email { get; set; }

        /// <summary>
        /// Имя пользователя (опционально).
        /// </summary>
        public string? UserName { get; set; }
    }
}


