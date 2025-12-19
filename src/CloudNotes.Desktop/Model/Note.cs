using System;
using System.Collections.Generic;

namespace CloudNotes.Desktop.Model;

public class Note
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }

    public bool IsFavorite { get; set; } = false;

    // ID заметки на сервере (null если еще не синхронизирована)
    public Guid? ServerId { get; set; }

    // Флаг синхронизации с сервером
    public bool IsSynced { get; set; } = false;

    public ICollection<NoteTag> NoteTags { get; set; } = new List<NoteTag>();
}
