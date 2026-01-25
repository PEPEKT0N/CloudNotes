using System;

namespace CloudNotes.Desktop.Model;

/// <summary>
/// Папка/каталог для организации заметок.
/// </summary>
public class Folder
{
    /// <summary>
    /// Уникальный идентификатор папки.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Название папки.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор родительской папки (null для корневых папок).
    /// </summary>
    public Guid? ParentFolderId { get; set; }

    /// <summary>
    /// Дата создания папки.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Дата последнего обновления папки.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    // ID папки на сервере (null если еще не синхронизирована)
    public Guid? ServerId { get; set; }

    // Флаг синхронизации с сервером
    public bool IsSynced { get; set; } = false;

    /// <summary>
    /// Email пользователя-владельца папки (для изоляции данных разных пользователей).
    /// </summary>
    public string? UserEmail { get; set; }
}
