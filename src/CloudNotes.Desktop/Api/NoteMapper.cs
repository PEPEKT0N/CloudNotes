using System;
using CloudNotes.Desktop.Api.DTOs;
using CloudNotes.Desktop.Model;

namespace CloudNotes.Desktop.Api;

public static class NoteMapper
{
    // Конвертирует API.NoteDto в локальную модель Note (с сервера)
    public static Note ToLocal(NoteDto dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        return new Note
        {
            Id = dto.Id,
            Title = dto.Title,
            Content = dto.Content ?? string.Empty,
            UpdatedAt = dto.UpdatedAt,
            IsFavorite = false, // IsFavorite не синхронизируется с сервером (локальное поле)
            ServerId = dto.Id, // ServerId = Id заметки на сервере
            IsSynced = true // Заметка синхронизирована, так как получена с сервера
        };
    }

    // Конвертирует локальную модель Note в API.NoteDto
    public static NoteDto ToDto(Note note, DateTime? createdAt = null, DateTime? syncedAt = null)
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
            SyncedAt = syncedAt
        };
    }

    // Конвертирует локальную модель Note в CreateNoteDto для создания на сервере
    public static CreateNoteDto ToCreateDto(Note note)
    {
        if (note == null)
            throw new ArgumentNullException(nameof(note));

        return new CreateNoteDto
        {
            Title = note.Title,
            Content = note.Content
        };
    }

    // Конвертирует локальную модель Note в UpdateNoteDto для обновления на сервере
    public static UpdateNoteDto ToUpdateDto(Note note, DateTime? clientUpdatedAt = null)
    {
        if (note == null)
            throw new ArgumentNullException(nameof(note));

        return new UpdateNoteDto
        {
            Title = note.Title,
            Content = note.Content,
            ClientUpdatedAt = clientUpdatedAt ?? note.UpdatedAt
        };
    }
}

