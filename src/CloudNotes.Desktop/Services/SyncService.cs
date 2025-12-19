using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CloudNotes.Desktop.Api;
using CloudNotes.Desktop.Api.DTOs;
using CloudNotes.Desktop.Model;
using CloudNotes.Services;
using Microsoft.Extensions.Logging;
using Refit;

namespace CloudNotes.Desktop.Services;

public class SyncService : ISyncService
{
    private readonly ICloudNotesApi _api;
    private readonly IAuthService _authService;
    private readonly INoteService _noteService;
    private readonly ILogger<SyncService>? _logger;
    private Timer? _periodicSyncTimer;
    private const int SyncIntervalMinutes = 5;
    private const int MaxRetryAttempts = 3;
    private const int BaseRetryDelaySeconds = 5;

    public SyncService(
        ICloudNotesApi api,
        IAuthService authService,
        INoteService noteService,
        ILogger<SyncService>? logger = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _noteService = noteService ?? throw new ArgumentNullException(nameof(noteService));
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
            // API ошибки (401, 403, 500 и т.д.) не требуют retry
            _logger?.LogError(ex, "Ошибка API при синхронизации: {StatusCode}", ex.StatusCode);
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
        // 1. Получаем все заметки с сервера
        var serverNotes = await _api.GetNotesAsync();

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
                    await _noteService.UpdateNoteAsync(localNote);
                    _logger?.LogInformation("Обновлена заметка {NoteId} с сервера", serverNote.Id);
                }
                else if (!localNote.IsSynced)
                {
                    // Локальная заметка не синхронизирована, но серверная версия не новее - просто обновляем ServerId и IsSynced
                    localNote.ServerId = serverNote.Id;
                    localNote.IsSynced = true;
                    await _noteService.UpdateNoteAsync(localNote);
                    _logger?.LogInformation("Синхронизирована заметка {NoteId}", serverNote.Id);
                }
            }
            else
            {
                // Новая заметка с сервера - создаем локально
                var newNote = NoteMapper.ToLocal(serverNote);
                await _noteService.CreateNoteAsync(newNote);
                _logger?.LogInformation("Создана заметка {NoteId} с сервера", serverNote.Id);
            }
        }

        // 4. Отправляем несинхронизированные локальные изменения на сервер (очередь несинхронизированных изменений)
        var unsyncedNotes = localNotes.Where(n => !n.IsSynced).ToList();
        
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
                        var updateDto = NoteMapper.ToUpdateDto(localNote, localNote.UpdatedAt);
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
                            _logger?.LogInformation("Обновлена заметка {NoteId} на сервере", localNote.ServerId.Value);
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                        {
                            // Конфликт - серверная версия новее
                            // TODO: Обработка конфликта (C3.1, C3.2)
                            _logger?.LogWarning("Конфликт при обновлении заметки {NoteId}: версия на сервере новее", localNote.ServerId.Value);
                        }
                    }
                    catch (ApiException ex)
                    {
                        _logger?.LogError(ex, "Ошибка при обновлении заметки {NoteId} на сервере", localNote.ServerId.Value);
                    }
                }
            }
            else
            {
                // Локальная заметка не существует на сервере - создаем
                try
                {
                    var createDto = NoteMapper.ToCreateDto(localNote);
                    var createdNote = await _api.CreateNoteAsync(createDto);
                    
                    // Обновляем локальную заметку с ServerId и помечаем как синхронизированную
                    localNote.ServerId = createdNote.Id;
                    localNote.IsSynced = true;
                    localNote.UpdatedAt = createdNote.UpdatedAt;
                    await _noteService.UpdateNoteAsync(localNote);
                    _logger?.LogInformation("Создана заметка {NoteId} на сервере", createdNote.Id);
                }
                catch (ApiException ex)
                {
                    _logger?.LogError(ex, "Ошибка при создании заметки {NoteId} на сервере", localNote.Id);
                }
            }
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
}

