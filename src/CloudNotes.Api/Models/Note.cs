using System.ComponentModel.DataAnnotations;

namespace CloudNotes.Api.Models;

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
    /// Идентификатор владельца заметки.
    /// </summary>
    public string UserId { get; set; } = null!;

    /// <summary>
    /// Владелец заметки.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Заголовок заметки.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Содержимое заметки (Markdown).
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Дата создания заметки.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Дата последнего обновления заметки.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Дата последней синхронизации с сервером.
    /// </summary>
    public DateTime? SyncedAt { get; set; }

    /// <summary>
    /// Флаг мягкого удаления (отложено, пока не используется).
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Идентификатор папки, в которой находится заметка (null если заметка не в папке).
    /// </summary>
    public Guid? FolderId { get; set; }

    /// <summary>
    /// Папка, в которой находится заметка.
    /// </summary>
    public Folder? Folder { get; set; }

    /// <summary>
    /// Теги заметки (Many-to-Many через NoteTag).
    /// </summary>
    public ICollection<NoteTag> NoteTags { get; set; } = new List<NoteTag>();
}

