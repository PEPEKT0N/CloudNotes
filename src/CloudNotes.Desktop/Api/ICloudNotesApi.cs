using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CloudNotes.Desktop.Api.DTOs;
using Refit;

namespace CloudNotes.Desktop.Api;

/// <summary>
/// Refit интерфейс для CloudNotes API.
/// </summary>
public interface ICloudNotesApi
{
    #region Auth

    /// <summary>
    /// Регистрация нового пользователя.
    /// </summary>
    /// <param name="dto">Данные для регистрации.</param>
    /// <returns>Токены доступа.</returns>
    [Post("/api/auth/register")]
    Task<TokenResponseDto> RegisterAsync([Body] RegisterDto dto);

    /// <summary>
    /// Вход пользователя.
    /// </summary>
    /// <param name="dto">Данные для входа.</param>
    /// <returns>Токены доступа.</returns>
    [Post("/api/auth/login")]
    Task<TokenResponseDto> LoginAsync([Body] LoginDto dto);

    /// <summary>
    /// Обновление access токена.
    /// </summary>
    /// <param name="dto">Refresh токен.</param>
    /// <returns>Новые токены.</returns>
    [Post("/api/auth/refresh")]
    Task<TokenResponseDto> RefreshAsync([Body] RefreshTokenDto dto);

    /// <summary>
    /// Выход (инвалидация refresh токена).
    /// </summary>
    /// <param name="dto">Refresh токен для инвалидации.</param>
    [Post("/api/auth/logout")]
    Task LogoutAsync([Body] RefreshTokenDto dto);

    #endregion

    #region Notes

    /// <summary>
    /// Получить все заметки текущего пользователя.
    /// </summary>
    /// <returns>Список заметок.</returns>
    [Get("/api/notes")]
    [Headers("Authorization: Bearer")]
    Task<IReadOnlyList<NoteDto>> GetNotesAsync();

    /// <summary>
    /// Получить заметку по ID.
    /// </summary>
    /// <param name="id">ID заметки.</param>
    /// <returns>Заметка.</returns>
    [Get("/api/notes/{id}")]
    [Headers("Authorization: Bearer")]
    Task<NoteDto> GetNoteByIdAsync(Guid id);

    /// <summary>
    /// Получить заметки по тегу.
    /// </summary>
    /// <param name="tag">Название тега.</param>
    /// <returns>Список заметок с указанным тегом.</returns>
    [Get("/api/notes/by-tag/{tag}")]
    [Headers("Authorization: Bearer")]
    Task<IReadOnlyList<NoteDto>> GetNotesByTagAsync(string tag);

    /// <summary>
    /// Поиск заметок по заголовку.
    /// </summary>
    /// <param name="title">Часть заголовка для поиска.</param>
    /// <returns>Список заметок.</returns>
    [Get("/api/notes/search")]
    [Headers("Authorization: Bearer")]
    Task<IReadOnlyList<NoteDto>> SearchNotesAsync([Query] string title);

    /// <summary>
    /// Создать новую заметку.
    /// </summary>
    /// <param name="dto">Данные заметки.</param>
    /// <returns>Созданная заметка.</returns>
    [Post("/api/notes")]
    [Headers("Authorization: Bearer")]
    Task<NoteDto> CreateNoteAsync([Body] CreateNoteDto dto);

    /// <summary>
    /// Обновить заметку.
    /// </summary>
    /// <param name="id">ID заметки.</param>
    /// <param name="dto">Новые данные заметки.</param>
    /// <returns>Обновлённая заметка.</returns>
    [Put("/api/notes/{id}")]
    [Headers("Authorization: Bearer")]
    Task<NoteDto> UpdateNoteAsync(Guid id, [Body] UpdateNoteDto dto);

    /// <summary>
    /// Обновить заметку (с возможностью получить конфликт).
    /// </summary>
    /// <param name="id">ID заметки.</param>
    /// <param name="dto">Новые данные заметки.</param>
    /// <returns>HTTP ответ с заметкой или конфликтом.</returns>
    [Put("/api/notes/{id}")]
    [Headers("Authorization: Bearer")]
    Task<IApiResponse<NoteDto>> UpdateNoteWithResponseAsync(Guid id, [Body] UpdateNoteDto dto);

    /// <summary>
    /// Удалить заметку.
    /// </summary>
    /// <param name="id">ID заметки.</param>
    [Delete("/api/notes/{id}")]
    [Headers("Authorization: Bearer")]
    Task DeleteNoteAsync(Guid id);

    #endregion

    #region Folders

    /// <summary>
    /// Получить все папки текущего пользователя.
    /// </summary>
    /// <returns>Список папок.</returns>
    [Get("/api/folders")]
    [Headers("Authorization: Bearer")]
    Task<IReadOnlyList<FolderDto>> GetFoldersAsync();

    /// <summary>
    /// Получить папку по ID.
    /// </summary>
    /// <param name="id">ID папки.</param>
    /// <returns>Папка.</returns>
    [Get("/api/folders/{id}")]
    [Headers("Authorization: Bearer")]
    Task<FolderDto> GetFolderByIdAsync(Guid id);

    /// <summary>
    /// Создать новую папку.
    /// </summary>
    /// <param name="dto">Данные папки.</param>
    /// <returns>Созданная папка.</returns>
    [Post("/api/folders")]
    [Headers("Authorization: Bearer")]
    Task<FolderDto> CreateFolderAsync([Body] CreateFolderDto dto);

    /// <summary>
    /// Обновить папку.
    /// </summary>
    /// <param name="id">ID папки.</param>
    /// <param name="dto">Новые данные папки.</param>
    /// <returns>Обновлённая папка.</returns>
    [Put("/api/folders/{id}")]
    [Headers("Authorization: Bearer")]
    Task<FolderDto> UpdateFolderAsync(Guid id, [Body] UpdateFolderDto dto);

    /// <summary>
    /// Удалить папку.
    /// </summary>
    /// <param name="id">ID папки.</param>
    [Delete("/api/folders/{id}")]
    [Headers("Authorization: Bearer")]
    Task DeleteFolderAsync(Guid id);

    #endregion
}

