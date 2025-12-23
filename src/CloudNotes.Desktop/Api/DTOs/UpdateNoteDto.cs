using System;
using System.Collections.Generic;

namespace CloudNotes.Desktop.Api.DTOs;

/// <summary>
/// DTO для обновления заметки.
/// </summary>
public class UpdateNoteDto
{
    /// <summary>
    /// Заголовок заметки.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Содержимое заметки (Markdown).
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Время последнего обновления на клиенте (для конфликт-резолвера).
    /// </summary>
    public DateTime? ClientUpdatedAt { get; set; }

    /// <summary>
    /// Названия тегов заметки.
    /// </summary>
    public IList<string> Tags { get; set; } = new List<string>();
}

