using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNotes.Desktop.Api;
using CloudNotes.Desktop.Api.DTOs;
using CloudNotes.Desktop.Data;
using CloudNotes.Desktop.Model;
using CloudNotes.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Refit;

namespace CloudNotes.Desktop.Services;

public class SyncService : ISyncService
{
    private readonly ICloudNotesApi _api;
    private readonly IAuthService _authService;
    private readonly INoteService _noteService;
    private readonly ITagService _tagService;
    private readonly IConflictService? _conflictService;
    private readonly ILogger<SyncService>? _logger;
    private Timer? _periodicSyncTimer;
    private const int SyncIntervalMinutes = 5;
    private const int MaxRetryAttempts = 3;
    private const int BaseRetryDelaySeconds = 5;

    public SyncService(
        ICloudNotesApi api,
        IAuthService authService,
        INoteService noteService,
        ITagService? tagService = null,
        IConflictService? conflictService = null,
        ILogger<SyncService>? logger = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _noteService = noteService ?? throw new ArgumentNullException(nameof(noteService));
        _tagService = tagService ?? new TagService(DbContextProvider.GetContext());
        _conflictService = conflictService;
        _logger = logger;
    }

    // Выполняет полную синхронизацию заметок с сервером: загружает новые/обновленные с сервера и отправляет локальные изменения
    public async Task<bool> SyncAsync()
    {
        // Проверка авторизации
        var isLoggedIn = await _authService.IsLoggedInAsync();
        if (!isLoggedIn)
        {
            _logger?.LogInformation("Пропуск синхронизации: пользователь не авторизован");
            return false;
        }

        try
        {
            return await PerformSyncAsync();
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "Сетевая ошибка при синхронизации");
            // Retry логика при сетевых ошибках
            return await RetrySyncAsync();
        }
        catch (ApiException ex)
        {
            // API ошибки (401, 403, 400, 500 и т.д.) не требуют retry
            _logger?.LogError(ex, "Ошибка API при синхронизации: {StatusCode}", ex.StatusCode);

            // Если 401 - токен истек, AuthHeaderHandler должен был обновить, но не получилось
            // Это означает, что refresh token тоже недействителен
            if (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                System.Diagnostics.Debug.WriteLine("SyncService: 401 Unauthorized - refresh token invalid, user needs to re-login");
                // Не пробрасываем исключение дальше, просто возвращаем false
                // UI должен обработать это и предложить перелогиниться
            }
            else if (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                // 400 Bad Request - проблема с данными запроса
                System.Diagnostics.Debug.WriteLine($"SyncService: 400 Bad Request - {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Content: {ex.Content}");
                _logger?.LogWarning("400 Bad Request при синхронизации. Возможно, проблема с данными заметки. Продолжаем работу без синхронизации.");
                // Не пробрасываем исключение, продолжаем работу локально
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Неожиданная ошибка при синхронизации");
            return false;
        }
    }

    // Основная логика синхронизации
    private async Task<bool> PerformSyncAsync()
    {
        // Получаем email текущего пользователя один раз
        var currentEmail = await _authService.GetCurrentUserEmailAsync();

        // 1. Получаем все заметки с сервера
        IReadOnlyList<NoteDto> serverNotes;
        try
        {
            serverNotes = await _api.GetNotesAsync();
        }
        catch (Refit.ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // 401 ошибка - токен истек
            // Пытаемся обновить токен и повторить запрос
            System.Diagnostics.Debug.WriteLine("SyncService: Got 401, attempting token refresh...");

            var newToken = await _authService.ForceRefreshTokenAsync();
            if (!string.IsNullOrEmpty(newToken))
            {
                System.Diagnostics.Debug.WriteLine("SyncService: Token refreshed, retrying GetNotesAsync...");
                // Повторяем запрос с новым токеном
                serverNotes = await _api.GetNotesAsync();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("SyncService: Token refresh failed, user needs to re-login");
                throw; // Пробрасываем дальше для обработки в UI
            }
        }

        // 2. Получаем все локальные заметки
        var localNotes = await _noteService.GetAllNoteAsync();
        var localNotesDict = localNotes.ToDictionary(n => n.Id);

        // 3. Синхронизация: загружаем новые/обновленные с сервера
        foreach (var serverNote in serverNotes)
        {
            // Ищем локальную заметку по ServerId или по Id
            var localNote = localNotes.FirstOrDefault(n => n.ServerId == serverNote.Id || n.Id == serverNote.Id);

            if (localNote != null)
            {
                // Заметка существует локально - проверяем, нужно ли обновить
                if (serverNote.UpdatedAt > localNote.UpdatedAt)
                {
                    // Серверная версия новее - обновляем локально
                    localNote.Title = serverNote.Title;
                    localNote.Content = serverNote.Content ?? string.Empty;
                    localNote.UpdatedAt = serverNote.UpdatedAt;
                    localNote.ServerId = serverNote.Id;
                    localNote.IsSynced = true;
                    localNote.UserEmail = currentEmail; // Обновляем UserEmail при синхронизации
                    await _noteService.UpdateNoteAsync(localNote);

                    // Синхронизируем теги с сервера
                    await SyncTagsFromServerAsync(localNote.Id, serverNote.Tags);

                    _logger?.LogInformation("Обновлена заметка {NoteId} с сервера", serverNote.Id);
                }
                else if (!localNote.IsSynced)
                {
                    // Локальная заметка не синхронизирована, но серверная версия не новее - просто обновляем ServerId и IsSynced
                    localNote.ServerId = serverNote.Id;
                    localNote.IsSynced = true;
                    localNote.UserEmail = currentEmail; // Обновляем UserEmail при синхронизации
                    await _noteService.UpdateNoteAsync(localNote);
                    _logger?.LogInformation("Синхронизирована заметка {NoteId}", serverNote.Id);
                }
            }
            else
            {
                // Новая заметка с сервера - создаем локально
                var newNote = NoteMapper.ToLocal(serverNote, currentEmail);
                await _noteService.CreateNoteAsync(newNote);

                // Синхронизируем теги с сервера
                await SyncTagsFromServerAsync(newNote.Id, serverNote.Tags);

                _logger?.LogInformation("Создана заметка {NoteId} с сервера", serverNote.Id);
            }
        }

        // 4. Удаляем локальные заметки текущего пользователя, которых нет на сервере
        var serverNoteIds = serverNotes.Select(sn => sn.Id).ToHashSet();
        var localNotesToDelete = localNotes
            .Where(n =>
                // Удаляем только заметки текущего пользователя
                n.UserEmail == currentEmail &&
                // Удаляем синхронизированные заметки, которых нет на сервере
                n.ServerId.HasValue && !serverNoteIds.Contains(n.ServerId.Value))
            .ToList();

        foreach (var noteToDelete in localNotesToDelete)
        {
            await _noteService.DeleteNoteAsync(noteToDelete.Id);
            _logger?.LogInformation("Удалена локальная заметка {NoteId}, которой нет на сервере", noteToDelete.Id);
        }

        // 5. Отправляем несинхронизированные локальные изменения на сервер (очередь несинхронизированных изменений)
        var unsyncedNotes = localNotes.Where(n => !n.IsSynced && !localNotesToDelete.Contains(n)).ToList();

        foreach (var localNote in unsyncedNotes)
        {
            // Если есть ServerId, значит заметка уже существует на сервере - обновляем
            if (localNote.ServerId.HasValue)
            {
                var serverNote = serverNotes.FirstOrDefault(sn => sn.Id == localNote.ServerId.Value);
                if (serverNote != null)
                {
                    // Локальная версия новее - обновляем на сервере
                    try
                    {
                        // Получаем теги локальной заметки
                        var localTags = await _tagService.GetTagsForNoteAsync(localNote.Id);
                        var tagNames = localTags.Select(t => t.Name).ToList();

                        var updateDto = NoteMapper.ToUpdateDto(localNote, localNote.UpdatedAt, tagNames);
                        var response = await _api.UpdateNoteWithResponseAsync(localNote.ServerId.Value, updateDto);

                        if (response.IsSuccessStatusCode)
                        {
                            // Обновление успешно
                            var updatedNote = response.Content!;
                            localNote.Title = updatedNote.Title;
                            localNote.Content = updatedNote.Content ?? string.Empty;
                            localNote.UpdatedAt = updatedNote.UpdatedAt;
                            localNote.IsSynced = true;
                            await _noteService.UpdateNoteAsync(localNote);

                            // Синхронизируем теги с сервера (на случай, если сервер изменил их)
                            await SyncTagsFromServerAsync(localNote.Id, updatedNote.Tags);

                            _logger?.LogInformation("Обновлена заметка {NoteId} на сервере", localNote.ServerId.Value);
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                        {
                            // Конфликт - серверная версия новее
                            // Refit выбрасывает ApiException при 409, поэтому обработка будет в catch блоке
                            _logger?.LogWarning("Конфликт при обновлении заметки {NoteId}: версия на сервере новее", localNote.ServerId.Value);
                        }
                    }
                    catch (ApiException ex)
                    {
                        // Обработка 409 Conflict из ApiException
                        if (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                        {
                            HandleConflictFromException(localNote, ex);
                        }
                        else if (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
                        {
                            // 400 Bad Request - проблема с данными заметки
                            System.Diagnostics.Debug.WriteLine($"400 Bad Request при обновлении заметки {localNote.ServerId.Value}: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"Content: {ex.Content}");
                            _logger?.LogWarning(ex, "400 Bad Request при обновлении заметки {NoteId} на сервере. Пропускаем эту заметку.", localNote.ServerId.Value);
                            // Пропускаем эту заметку, продолжаем синхронизацию остальных
                        }
                        else
                        {
                            _logger?.LogError(ex, "Ошибка при обновлении заметки {NoteId} на сервере", localNote.ServerId.Value);
                        }
                    }
                }
            }
            else
            {
                // Локальная заметка не существует на сервере - создаем
                try
                {
                    // Получаем теги локальной заметки
                    var localTags = await _tagService.GetTagsForNoteAsync(localNote.Id);
                    var tagNames = localTags.Select(t => t.Name).ToList();

                    var createDto = NoteMapper.ToCreateDto(localNote, tagNames);
                    var createdNote = await _api.CreateNoteAsync(createDto);

                    // Обновляем локальную заметку с ServerId и помечаем как синхронизированную
                    localNote.ServerId = createdNote.Id;
                    localNote.IsSynced = true;
                    localNote.UpdatedAt = createdNote.UpdatedAt;
                    await _noteService.UpdateNoteAsync(localNote);

                    // Синхронизируем теги с сервера (на случай, если сервер создал новые теги)
                    await SyncTagsFromServerAsync(localNote.Id, createdNote.Tags);

                    _logger?.LogInformation("Создана заметка {NoteId} на сервере", createdNote.Id);
                }
                catch (ApiException ex)
                {
                    if (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
                    {
                        // 400 Bad Request - проблема с данными заметки
                        System.Diagnostics.Debug.WriteLine($"400 Bad Request при создании заметки {localNote.Id}: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Content: {ex.Content}");
                        _logger?.LogWarning(ex, "400 Bad Request при создании заметки {NoteId} на сервере. Пропускаем эту заметку.", localNote.Id);
                        // Пропускаем эту заметку, продолжаем синхронизацию остальных
                    }
                    else
                    {
                        _logger?.LogError(ex, "Ошибка при создании заметки {NoteId} на сервере", localNote.Id);
                    }
                }
            }
        }

        // 6. Синхронизируем папки
        try
        {
            var folderService = new FolderService(
                DbContextProvider.GetContext(),
                _api,
                _authService,
                null); // Логгер не обязателен для FolderService
            await folderService.SyncFoldersAsync();
            _logger?.LogInformation("Синхронизация папок завершена");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при синхронизации папок");
            // Продолжаем выполнение даже если синхронизация папок не удалась
        }

        _logger?.LogInformation("Синхронизация завершена успешно");
        return true;
    }

    // Retry логика при сетевых ошибках с экспоненциальным backoff
    private async Task<bool> RetrySyncAsync()
    {
        for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            var delay = TimeSpan.FromSeconds(BaseRetryDelaySeconds * Math.Pow(2, attempt - 1));
            _logger?.LogInformation("Повторная попытка синхронизации {Attempt}/{MaxAttempts} через {Delay} секунд", attempt, MaxRetryAttempts, delay.TotalSeconds);

            await Task.Delay(delay);

            try
            {
                // Повторная попытка синхронизации
                return await PerformSyncAsync();
            }
            catch (HttpRequestException retryEx)
            {
                _logger?.LogWarning(retryEx, "Retry попытка {Attempt} не удалась", attempt);
                if (attempt == MaxRetryAttempts)
                {
                    _logger?.LogError("Все retry попытки исчерпаны. Синхронизация отложена до следующего цикла");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Неожиданная ошибка при retry попытке {Attempt}", attempt);
                return false;
            }
        }

        return false;
    }

    // Синхронизация при запуске приложения (если пользователь авторизован)
    public async Task<bool> SyncOnStartupAsync()
    {
        var isLoggedIn = await _authService.IsLoggedInAsync();
        if (!isLoggedIn)
        {
            _logger?.LogInformation("Пропуск синхронизации при запуске: пользователь не авторизован");
            return false;
        }

        return await SyncAsync();
    }

    // Запускает периодическую фоновую синхронизацию с интервалом 5 минут
    public void StartPeriodicSync()
    {
        if (_periodicSyncTimer != null)
        {
            _logger?.LogWarning("Периодическая синхронизация уже запущена");
            return;
        }

        var interval = TimeSpan.FromMinutes(SyncIntervalMinutes);
        _periodicSyncTimer = new Timer(async _ => await SyncAsync(), null, interval, interval);
        _logger?.LogInformation("Запущена периодическая синхронизация (интервал: {Interval} минут)", SyncIntervalMinutes);
    }

    // Останавливает периодическую фоновую синхронизацию
    public void StopPeriodicSync()
    {
        if (_periodicSyncTimer == null)
        {
            return;
        }

        _periodicSyncTimer.Dispose();
        _periodicSyncTimer = null;
        _logger?.LogInformation("Остановлена периодическая синхронизация");
    }

    // Обработка конфликта из строки контента
    private void HandleConflictFromContent(Note localNote, string content)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var conflictResponse = JsonSerializer.Deserialize<ConflictResponseDto>(content, options);

            if (conflictResponse?.ServerNote != null)
            {
                var conflict = new NoteConflict
                {
                    LocalNoteId = localNote.Id,
                    LocalNote = localNote,
                    ServerNote = conflictResponse.ServerNote,
                    DetectedAt = DateTime.UtcNow
                };

                _conflictService?.AddConflict(conflict);
                _logger?.LogWarning("Обнаружен конфликт для заметки {NoteId}. Серверная версия новее", localNote.Id);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при обработке конфликта для заметки {NoteId}", localNote.Id);
        }
    }

    // Обработка конфликта из ApiException
    private void HandleConflictFromException(Note localNote, ApiException ex)
    {
        if (ex.Content != null)
        {
            HandleConflictFromContent(localNote, ex.Content);
        }
    }

    /// <summary>
    /// Синхронизирует теги заметки с сервера: создает локальные теги и связи, если их нет.
    /// </summary>
    /// <param name="noteId">ID локальной заметки.</param>
    /// <param name="serverTagNames">Список названий тегов с сервера.</param>
    private async Task SyncTagsFromServerAsync(Guid noteId, IList<string> serverTagNames)
    {
        // Получаем текущие теги заметки
        var existingTags = await _tagService.GetTagsForNoteAsync(noteId);

        if (serverTagNames == null || serverTagNames.Count == 0)
        {
            // Если на сервере нет тегов, удаляем все локальные связи с тегами
            foreach (var tag in existingTags)
            {
                await _tagService.RemoveTagFromNoteAsync(noteId, tag.Id);
            }
            return;
        }
        var existingTagNames = existingTags.Select(t => t.Name.ToLowerInvariant()).ToHashSet();
        var serverTagNamesLower = serverTagNames.Select(t => t.ToLowerInvariant()).ToHashSet();

        // Удаляем теги, которых больше нет на сервере
        var tagsToRemove = existingTags.Where(t => !serverTagNamesLower.Contains(t.Name.ToLowerInvariant())).ToList();
        foreach (var tag in tagsToRemove)
        {
            await _tagService.RemoveTagFromNoteAsync(noteId, tag.Id);
        }

        // Добавляем новые теги с сервера
        var tagsToAdd = serverTagNames.Where(tn => !existingTagNames.Contains(tn.ToLowerInvariant())).ToList();
        foreach (var tagName in tagsToAdd)
        {
            // Получаем или создаем тег локально
            var tag = await _tagService.GetOrCreateTagAsync(tagName);
            await _tagService.AddTagToNoteAsync(noteId, tag.Id);
        }

        _logger?.LogInformation("Синхронизированы теги для заметки {NoteId}", noteId);
    }
}

