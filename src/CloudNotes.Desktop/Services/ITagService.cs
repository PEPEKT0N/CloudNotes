using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CloudNotes.Desktop.Model;

namespace CloudNotes.Desktop.Services;

/// <summary>
/// Интерфейс сервиса для работы с тегами.
/// </summary>
public interface ITagService
{
    /// <summary>
    /// Получает все теги.
    /// </summary>
    Task<IEnumerable<Tag>> GetAllTagsAsync();

    /// <summary>
    /// Получает тег по идентификатору.
    /// </summary>
    Task<Tag?> GetTagByIdAsync(Guid id);

    /// <summary>
    /// Получает тег по имени.
    /// </summary>
    Task<Tag?> GetTagByNameAsync(string name);

    /// <summary>
    /// Создаёт новый тег.
    /// </summary>
    Task<Tag> CreateTagAsync(Tag tag);

    /// <summary>
    /// Получает или создаёт тег по имени.
    /// </summary>
    Task<Tag> GetOrCreateTagAsync(string name);

    /// <summary>
    /// Удаляет тег.
    /// </summary>
    Task<bool> DeleteTagAsync(Guid id);

    /// <summary>
    /// Получает теги для заметки.
    /// </summary>
    Task<IEnumerable<Tag>> GetTagsForNoteAsync(Guid noteId);

    /// <summary>
    /// Добавляет тег к заметке.
    /// </summary>
    Task AddTagToNoteAsync(Guid noteId, Guid tagId);

    /// <summary>
    /// Удаляет тег из заметки.
    /// </summary>
    Task RemoveTagFromNoteAsync(Guid noteId, Guid tagId);

    /// <summary>
    /// Получает заметки с указанным тегом.
    /// </summary>
    Task<IEnumerable<Note>> GetNotesWithTagAsync(Guid tagId);

    /// <summary>
    /// Получает все карточки из заметок с указанными тегами.
    /// </summary>
    Task<List<(Guid NoteId, Flashcard Card)>> GetFlashcardsByTagsAsync(List<Guid> tagIds);
}
