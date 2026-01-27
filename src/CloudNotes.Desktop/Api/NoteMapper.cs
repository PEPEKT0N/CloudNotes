using System;
using System.Collections.Generic;
using System.Linq;
using CloudNotes.Desktop.Api.DTOs;
using CloudNotes.Desktop.Model;

namespace CloudNotes.Desktop.Api;

public static class NoteMapper
{
    // Конвертирует API.NoteDto в локальную модель Note (с сервера)
    public static Note ToLocal(NoteDto dto, string? userEmail = null)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        return new Note
        {
            Id = dto.Id,
            Title = dto.Title,
            Content = dto.Content ?? string.Empty,
            CreatedAt = dto.CreatedAt,
            UpdatedAt = dto.UpdatedAt,
            IsFavorite = false, // IsFavorite не синхронизируется с сервером (локальное поле)
            ServerId = dto.Id, // ServerId = Id заметки на сервере
            IsSynced = true, // Заметка синхронизирована, так как получена с сервера
            FolderId = dto.FolderId,
            UserEmail = userEmail // Сохраняем email пользователя для изоляции данных
        };
    }

    // Конвертирует локальную модель Note в API.NoteDto
    public static NoteDto ToDto(Note note, DateTime? createdAt = null, DateTime? syncedAt = null, IList<string>? tags = null)
    {
        if (note == null)
            throw new ArgumentNullException(nameof(note));

        return new NoteDto
        {
            Id = note.Id,
            Title = note.Title,
            Content = note.Content,
            CreatedAt = createdAt ?? note.UpdatedAt,
            UpdatedAt = note.UpdatedAt,
            SyncedAt = syncedAt,
            FolderId = note.FolderId,
            Tags = tags ?? new List<string>()
        };
    }

    // Конвертирует локальную модель Note в CreateNoteDto для создания на сервере
    public static CreateNoteDto ToCreateDto(Note note, IList<string>? tags = null)
    {
        if (note == null)
            throw new ArgumentNullException(nameof(note));

        return new CreateNoteDto
        {
            Title = note.Title,
            Content = note.Content,
            FolderId = note.FolderId,
            Tags = tags ?? new List<string>()
        };
    }

    // Конвертирует локальную модель Note в UpdateNoteDto для обновления на сервере
    public static UpdateNoteDto ToUpdateDto(Note note, DateTime? clientUpdatedAt = null, IList<string>? tags = null)
    {
        if (note == null)
            throw new ArgumentNullException(nameof(note));

        return new UpdateNoteDto
        {
            Title = note.Title,
            Content = note.Content,
            ClientUpdatedAt = clientUpdatedAt ?? note.UpdatedAt,
            FolderId = note.FolderId,
            Tags = tags ?? new List<string>()
        };
    }

    // Извлекает названия тегов из коллекции NoteTag
    public static IList<string> ExtractTagNames(IEnumerable<NoteTag> noteTags)
    {
        return noteTags?.Select(nt => nt.Tag.Name).ToList() ?? new List<string>();
    }
}

