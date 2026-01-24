using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudNotes.Desktop.Api;
using CloudNotes.Desktop.Api.DTOs;
using CloudNotes.Desktop.Data;
using CloudNotes.Desktop.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CloudNotes.Desktop.Services;

/// <summary>
/// Сервис для работы с папками на Desktop.
/// </summary>
public class FolderService
{
    private readonly Func<AppDbContext> _contextFactory;
    private readonly ICloudNotesApi? _api;
    private readonly IAuthService? _authService;
    private readonly ILogger<FolderService>? _logger;

    public FolderService(AppDbContext context, ICloudNotesApi? api = null, IAuthService? authService = null, ILogger<FolderService>? logger = null)
    {
        // Используем фабрику для создания нового контекста для каждой операции
        // Это обеспечивает thread-safety
        _contextFactory = CloudNotes.Services.DbContextProvider.CreateContext;
        _api = api;
        _authService = authService;
        _logger = logger;
    }

    private AppDbContext CreateContext() => _contextFactory();

    /// <summary>
    /// Получить все папки из локальной БД.
    /// </summary>
    public async Task<IEnumerable<Folder>> GetAllFoldersAsync()
    {
        using var context = CreateContext();
        
        // Фильтруем папки по текущему пользователю
        var query = context.Folders.AsQueryable();

        if (_authService != null)
        {
            var isAuthenticated = await _authService.IsLoggedInAsync();
            if (isAuthenticated)
            {
                var currentEmail = await _authService.GetCurrentUserEmailAsync();
                if (currentEmail != null)
                {
                    // Показываем только папки текущего пользователя
                    query = query.Where(f => f.UserEmail == currentEmail);
                }
                else
                {
                    // Если пользователь не авторизован, не показываем папки с UserEmail
                    query = query.Where(f => f.UserEmail == null);
                }
            }
            else
            {
                // Если пользователь не авторизован, не показываем папки с UserEmail
                query = query.Where(f => f.UserEmail == null);
            }
        }
        else
        {
            // Если AuthService недоступен, показываем только папки без UserEmail (гостевые)
            query = query.Where(f => f.UserEmail == null);
        }

        return await query
            .OrderBy(f => f.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Получить папку по ID.
    /// </summary>
    public async Task<Folder?> GetFolderByIdAsync(Guid id)
    {
        using var context = CreateContext();
        return await context.Folders.FindAsync(id);
    }

    /// <summary>
    /// Создать новую папку локально.
    /// </summary>
    public async Task<Folder> CreateFolderAsync(Folder folder)
    {
        using var context = CreateContext();
        
        if (!folder.ServerId.HasValue)
        {
            folder.IsSynced = false;
        }

        // Устанавливаем UserEmail для текущего пользователя
        if (_authService != null)
        {
            var isAuthenticated = await _authService.IsLoggedInAsync();
            if (isAuthenticated)
            {
                var currentEmail = await _authService.GetCurrentUserEmailAsync();
                folder.UserEmail = currentEmail;
            }
        }

        context.Folders.Add(folder);
        await context.SaveChangesAsync();

        return folder;
    }

    /// <summary>
    /// Обновить папку локально.
    /// </summary>
    public async Task<bool> UpdateFolderAsync(Folder folder)
    {
        using var context = CreateContext();
        
        var existingFolder = await context.Folders.FindAsync(folder.Id);
        if (existingFolder == null)
        {
            return false;
        }

        var wasSynced = existingFolder.IsSynced;

        existingFolder.Name = folder.Name;
        existingFolder.ParentFolderId = folder.ParentFolderId;
        existingFolder.ServerId = folder.ServerId;
        existingFolder.IsSynced = folder.IsSynced;

        if (wasSynced && !folder.IsSynced)
        {
            existingFolder.IsSynced = false;
        }

        context.Entry(existingFolder).State = EntityState.Modified;
        await context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Удалить папку локально.
    /// </summary>
    public async Task<bool> DeleteFolderAsync(Guid id)
    {
        using var context = CreateContext();
        
        var folder = await context.Folders.FindAsync(id);
        if (folder == null)
        {
            return false;
        }

        context.Folders.Remove(folder);
        await context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Синхронизировать папки с сервером.
    /// </summary>
    public async Task<bool> SyncFoldersAsync()
    {
        if (_api == null || _authService == null)
        {
            return false;
        }

        var isAuthenticated = await _authService.IsLoggedInAsync();
        if (!isAuthenticated)
        {
            return false;
        }

        try
        {
            // Получаем папки с сервера
            var serverFolders = await _api.GetFoldersAsync();

            // Получаем локальные папки
            using var context = CreateContext();
            var localFolders = await context.Folders.ToListAsync();

            // Создаем словарь для быстрого поиска
            var localFoldersByServerId = localFolders
                .Where(f => f.ServerId.HasValue)
                .ToDictionary(f => f.ServerId!.Value);

            // Получаем email текущего пользователя один раз
            var currentEmail = await _authService.GetCurrentUserEmailAsync();

            // Обновляем существующие и добавляем новые
            foreach (var serverFolder in serverFolders)
            {
                if (localFoldersByServerId.TryGetValue(serverFolder.Id, out var localFolder))
                {
                    // Обновляем существующую папку
                    localFolder.Name = serverFolder.Name;
                    localFolder.ParentFolderId = serverFolder.ParentFolderId;
                    localFolder.CreatedAt = serverFolder.CreatedAt;
                    localFolder.UpdatedAt = serverFolder.UpdatedAt;
                    localFolder.IsSynced = true;
                    localFolder.UserEmail = currentEmail; // Обновляем UserEmail при синхронизации
                }
                else
                {
                    // Добавляем новую папку
                    var newFolder = new Folder
                    {
                        Id = Guid.NewGuid(),
                        Name = serverFolder.Name,
                        ParentFolderId = serverFolder.ParentFolderId,
                        CreatedAt = serverFolder.CreatedAt,
                        UpdatedAt = serverFolder.UpdatedAt,
                        ServerId = serverFolder.Id,
                        IsSynced = true,
                        UserEmail = currentEmail // Сохраняем email пользователя для изоляции данных
                    };
                    context.Folders.Add(newFolder);
                }
            }

            // Удаляем локальные папки текущего пользователя, которых нет на сервере
            var serverFolderIds = serverFolders.Select(sf => sf.Id).ToHashSet();
            var localFoldersToDelete = localFolders
                .Where(f => 
                    // Удаляем только папки текущего пользователя
                    f.UserEmail == currentEmail &&
                    // Удаляем синхронизированные папки, которых нет на сервере
                    f.ServerId.HasValue && !serverFolderIds.Contains(f.ServerId.Value))
                .ToList();

            foreach (var folderToDelete in localFoldersToDelete)
            {
                context.Folders.Remove(folderToDelete);
                _logger?.LogInformation("Удалена локальная папка {FolderId}, которой нет на сервере", folderToDelete.Id);
            }

            // Отправляем несинхронизированные локальные папки на сервер
            var unsyncedFolders = localFolders.Where(f => !f.IsSynced && !localFoldersToDelete.Contains(f)).ToList();
            foreach (var unsyncedFolder in unsyncedFolders)
            {
                try
                {
                    FolderDto createdFolder;
                    if (unsyncedFolder.ServerId.HasValue)
                    {
                        // Обновляем существующую папку на сервере
                        var updateDto = new UpdateFolderDto
                        {
                            Name = unsyncedFolder.Name,
                            ParentFolderId = unsyncedFolder.ParentFolderId
                        };
                        createdFolder = await _api.UpdateFolderAsync(unsyncedFolder.ServerId.Value, updateDto);
                    }
                    else
                    {
                        // Создаем новую папку на сервере
                        var createDto = new CreateFolderDto
                        {
                            Name = unsyncedFolder.Name,
                            ParentFolderId = unsyncedFolder.ParentFolderId
                        };
                        createdFolder = await _api.CreateFolderAsync(createDto);
                    }

                    // Обновляем локальную папку
                    unsyncedFolder.ServerId = createdFolder.Id;
                    unsyncedFolder.IsSynced = true;
                    unsyncedFolder.CreatedAt = createdFolder.CreatedAt;
                    unsyncedFolder.UpdatedAt = createdFolder.UpdatedAt;
                }
                catch
                {
                    // Игнорируем ошибки синхронизации отдельных папок
                }
            }

            await context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
