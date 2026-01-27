using System;

namespace CloudNotes.Desktop.Model;

/// <summary>
/// Связующая таблица для Many-to-Many между Note и Tag.
/// </summary>
public class NoteTag
{
    /// <summary>
    /// Идентификатор заметки.
    /// </summary>
    public Guid NoteId { get; set; }

    /// <summary>
    /// Заметка.
    /// </summary>
    public Note Note { get; set; } = null!;

    /// <summary>
    /// Идентификатор тега.
    /// </summary>
    public Guid TagId { get; set; }

    /// <summary>
    /// Тег.
    /// </summary>
    public Tag Tag { get; set; } = null!;
}

