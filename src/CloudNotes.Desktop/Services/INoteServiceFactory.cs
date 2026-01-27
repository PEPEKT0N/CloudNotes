using System;
using System.Threading.Tasks;

namespace CloudNotes.Desktop.Services;

/// <summary>
/// Фабрика для переключения между гостевым и авторизованным режимами работы с заметками.
/// </summary>
public interface INoteServiceFactory
{
    /// <summary>
    /// Текущий активный сервис заметок.
    /// </summary>
    INoteService CurrentNoteService { get; }

    /// <summary>
    /// Текущий активный сервис тегов.
    /// </summary>
    ITagService CurrentTagService { get; }

    /// <summary>
    /// Гостевой сервис заметок (InMemory).
    /// </summary>
    GuestNoteService GuestNoteService { get; }

    /// <summary>
    /// Гостевой сервис тегов (InMemory).
    /// </summary>
    GuestTagService GuestTagService { get; }

    /// <summary>
    /// Авторизованный сервис заметок (SQLite).
    /// </summary>
    INoteService AuthenticatedNoteService { get; }

    /// <summary>
    /// Авторизованный сервис тегов (SQLite).
    /// </summary>
    ITagService AuthenticatedTagService { get; }

    /// <summary>
    /// Находится ли приложение в гостевом режиме.
    /// </summary>
    bool IsGuestMode { get; }

    /// <summary>
    /// Переключиться в гостевой режим.
    /// </summary>
    void SwitchToGuestMode();

    /// <summary>
    /// Переключиться в авторизованный режим.
    /// </summary>
    void SwitchToAuthenticatedMode();

    /// <summary>
    /// Очистить локальную БД (для использования при авторизации нового пользователя).
    /// </summary>
    Task ClearLocalDatabaseAsync();

    /// <summary>
    /// Событие, вызываемое при переключении режима.
    /// </summary>
    event Action<bool>? ModeChanged;
}
