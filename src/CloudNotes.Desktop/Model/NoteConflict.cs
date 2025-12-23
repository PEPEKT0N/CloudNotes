using System;
using CloudNotes.Desktop.Api.DTOs;

namespace CloudNotes.Desktop.Model;

// Модель конфликта заметки (локальная vs серверная версия)
public class NoteConflict
{
    public Guid LocalNoteId { get; set; }

    public Note LocalNote { get; set; } = null!;

    public NoteDto ServerNote { get; set; } = null!;

    public DateTime DetectedAt { get; set; } = DateTime.Now;
}

