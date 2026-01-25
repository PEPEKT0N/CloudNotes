using System;
using System.Collections.Generic;

namespace CloudNotes.Desktop.Model;

public class Note
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Дата создания заметки.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Дата последнего обновления.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    public bool IsFavorite { get; set; } = false;

    // ID заметки на сервере (null если еще не синхронизирована)
    public Guid? ServerId { get; set; }

    // Флаг синхронизации с сервером
    public bool IsSynced { get; set; } = false;

    /// <summary>
    /// Идентификатор папки, в которой находится заметка (null если заметка не в папке).
    /// </summary>
    public Guid? FolderId { get; set; }

    /// <summary>
    /// Email пользователя-владельца заметки (для изоляции данных разных пользователей).
    /// </summary>
    public string? UserEmail { get; set; }

    public ICollection<NoteTag> NoteTags { get; set; } = new List<NoteTag>();
}
