using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudNotes.Desktop.Data;
using CloudNotes.Desktop.Model;
using Microsoft.EntityFrameworkCore;

namespace CloudNotes.Desktop.Services;

/// <summary>
/// Сервис для работы с тегами.
/// </summary>
public class TagService : ITagService
{
    private readonly Func<AppDbContext> _contextFactory;

    public TagService(AppDbContext context)
    {
        // Используем фабрику для создания нового контекста для каждой операции
        // Это обеспечивает thread-safety
        _contextFactory = CloudNotes.Services.DbContextProvider.CreateContext;
    }

    private AppDbContext CreateContext() => _contextFactory();

    /// <inheritdoc />
    public async Task<IEnumerable<Tag>> GetAllTagsAsync()
    {
        using var context = CreateContext();
        return await context.Tags.ToListAsync();
    }

    /// <inheritdoc />
    public async Task<Tag?> GetTagByIdAsync(Guid id)
    {
        using var context = CreateContext();
        return await context.Tags.FindAsync(id);
    }

    /// <inheritdoc />
    public async Task<Tag?> GetTagByNameAsync(string name)
    {
        using var context = CreateContext();
        return await context.Tags
            .FirstOrDefaultAsync(t => t.Name.ToLower() == name.ToLower());
    }

    /// <inheritdoc />
    public async Task<Tag> CreateTagAsync(Tag tag)
    {
        using var context = CreateContext();

        if (tag.Id == Guid.Empty)
        {
            tag.Id = Guid.NewGuid();
        }

        context.Tags.Add(tag);
        await context.SaveChangesAsync();
        return tag;
    }

    /// <inheritdoc />
    public async Task<Tag> GetOrCreateTagAsync(string name)
    {
        var existingTag = await GetTagByNameAsync(name);
        if (existingTag != null)
        {
            return existingTag;
        }

        var newTag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = name.Trim()
        };

        return await CreateTagAsync(newTag);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteTagAsync(Guid id)
    {
        using var context = CreateContext();

        var tag = await context.Tags.FindAsync(id);
        if (tag == null)
        {
            return false;
        }

        context.Tags.Remove(tag);
        await context.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Tag>> GetTagsForNoteAsync(Guid noteId)
    {
        using var context = CreateContext();
        return await context.NoteTags
            .Where(nt => nt.NoteId == noteId)
            .Include(nt => nt.Tag)
            .Select(nt => nt.Tag)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task AddTagToNoteAsync(Guid noteId, Guid tagId)
    {
        using var context = CreateContext();

        var existingLink = await context.NoteTags
            .FirstOrDefaultAsync(nt => nt.NoteId == noteId && nt.TagId == tagId);

        if (existingLink != null)
        {
            return; // Связь уже существует
        }

        var noteTag = new NoteTag
        {
            NoteId = noteId,
            TagId = tagId
        };

        context.NoteTags.Add(noteTag);
        await context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task RemoveTagFromNoteAsync(Guid noteId, Guid tagId)
    {
        using var context = CreateContext();

        var noteTag = await context.NoteTags
            .FirstOrDefaultAsync(nt => nt.NoteId == noteId && nt.TagId == tagId);

        if (noteTag == null)
        {
            return;
        }

        context.NoteTags.Remove(noteTag);
        await context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Note>> GetNotesWithTagAsync(Guid tagId)
    {
        using var context = CreateContext();
        return await context.NoteTags
            .Where(nt => nt.TagId == tagId)
            .Include(nt => nt.Note)
            .Select(nt => nt.Note)
            .ToListAsync();
    }

    /// <summary>
    /// Получает все карточки из заметок с указанными тегами.
    /// </summary>
    /// <param name="tagIds">Список ID тегов.</param>
    /// <returns>Список кортежей (NoteId, Flashcard).</returns>
    public async Task<List<(Guid NoteId, Flashcard Card)>> GetFlashcardsByTagsAsync(List<Guid> tagIds)
    {
        if (tagIds == null || tagIds.Count == 0)
        {
            return new List<(Guid, Flashcard)>();
        }

        using var context = CreateContext();

        // Получаем заметки, у которых есть хотя бы один из указанных тегов
        var notes = await context.Notes
            .Include(n => n.NoteTags)
            .Where(n => n.NoteTags.Any(nt => tagIds.Contains(nt.TagId)))
            .ToListAsync();

        var result = new List<(Guid NoteId, Flashcard Card)>();

        foreach (var note in notes)
        {
            var cards = FlashcardParser.Parse(note.Content);
            foreach (var card in cards)
            {
                result.Add((note.Id, card));
            }
        }

        return result;
    }
}
