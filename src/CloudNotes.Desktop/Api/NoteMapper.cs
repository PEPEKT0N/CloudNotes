using System;
using CloudNotes.Desktop.Api.DTOs;
using CloudNotes.Desktop.Model;

namespace CloudNotes.Desktop.Api;

/// <summary>
/// Маппер для конвертации между локальной моделью Note и API DTOs.
/// </summary>
public static class NoteMapper
{
    /// <summary>
    /// Конвертирует API.NoteDto в локальную модель Note.
    /// </summary>
    /// <param name="dto">DTO от сервера.</param>
    /// <returns>Локальная модель Note.</returns>
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
            IsFavorite = false // IsFavorite не синхронизируется с сервером (локальное поле)
        };
    }

    /// <summary>
    /// Конвертирует локальную модель Note в API.NoteDto.
    /// </summary>
    /// <param name="note">Локальная модель Note.</param>
    /// <param name="createdAt">Дата создания (если известна).</param>
    /// <param name="syncedAt">Дата последней синхронизации (если известна).</param>
    /// <returns>DTO для отправки на сервер.</returns>
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

    /// <summary>
    /// Конвертирует локальную модель Note в CreateNoteDto для создания на сервере.
    /// </summary>
    /// <param name="note">Локальная модель Note.</param>
    /// <returns>DTO для создания заметки.</returns>
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

    /// <summary>
    /// Конвертирует локальную модель Note в UpdateNoteDto для обновления на сервере.
    /// </summary>
    /// <param name="note">Локальная модель Note.</param>
    /// <param name="clientUpdatedAt">Время последнего обновления на клиенте (для конфликт-резолвера).</param>
    /// <returns>DTO для обновления заметки.</returns>
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

