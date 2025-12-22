using System;
using System.Collections.Generic;

namespace CloudNotes.Desktop.Model;

/// <summary>
/// Заметка пользователя.
/// </summary>
public class Note
{
    /// <summary>
    /// Уникальный идентификатор заметки.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Заголовок заметки.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Содержимое заметки (Markdown).
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Дата создания заметки.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Дата последнего обновления.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Флаг избранного.
    /// </summary>
    public bool IsFavorite { get; set; } = false;

    /// <summary>
    /// Теги заметки (Many-to-Many через NoteTag).
    /// </summary>
    public ICollection<NoteTag> NoteTags { get; set; } = new List<NoteTag>();
}
