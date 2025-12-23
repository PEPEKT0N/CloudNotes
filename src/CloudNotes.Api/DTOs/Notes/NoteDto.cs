namespace CloudNotes.Api.DTOs.Notes;

/// <summary>
/// DTO для представления заметки.
/// </summary>
public class NoteDto
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
    public string? Content { get; set; }

    /// <summary>
    /// Дата создания заметки.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Дата последнего обновления заметки.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Дата последней синхронизации с сервером.
    /// </summary>
    public DateTime? SyncedAt { get; set; }

    /// <summary>
    /// Названия тегов заметки.
    /// </summary>
    public IList<string> Tags { get; set; } = new List<string>();
}

