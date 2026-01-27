using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudNotes.Desktop.Model;

namespace CloudNotes.Desktop.Services;

/// <summary>
/// InMemory реализация ITagService для гостевого режима.
/// Теги хранятся только в памяти и не сохраняются в БД.
/// </summary>
public class GuestTagService : ITagService
{
    private readonly List<Tag> _tags = new();
    private readonly List<NoteTag> _noteTags = new();
    private readonly GuestNoteService _guestNoteService;
    private readonly object _lock = new();

    public GuestTagService(GuestNoteService guestNoteService)
    {
        _guestNoteService = guestNoteService ?? throw new ArgumentNullException(nameof(guestNoteService));
    }

    /// <inheritdoc />
    public Task<IEnumerable<Tag>> GetAllTagsAsync()
    {
        lock (_lock)
        {
            var tagsCopy = _tags.Select(t => new Tag
            {
                Id = t.Id,
                Name = t.Name
            }).ToList();

            return Task.FromResult<IEnumerable<Tag>>(tagsCopy);
        }
    }

    /// <inheritdoc />
    public Task<Tag?> GetTagByIdAsync(Guid id)
    {
        lock (_lock)
        {
            var tag = _tags.FirstOrDefault(t => t.Id == id);
            if (tag == null) return Task.FromResult<Tag?>(null);

            return Task.FromResult<Tag?>(new Tag
            {
                Id = tag.Id,
                Name = tag.Name
            });
        }
    }

    /// <inheritdoc />
    public Task<Tag?> GetTagByNameAsync(string name)
    {
        lock (_lock)
        {
            var tag = _tags.FirstOrDefault(t =>
                t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (tag == null) return Task.FromResult<Tag?>(null);

            return Task.FromResult<Tag?>(new Tag
            {
                Id = tag.Id,
                Name = tag.Name
            });
        }
    }

    /// <inheritdoc />
    public Task<Tag> CreateTagAsync(Tag tag)
    {
        lock (_lock)
        {
            var newTag = new Tag
            {
                Id = tag.Id == Guid.Empty ? Guid.NewGuid() : tag.Id,
                Name = tag.Name.Trim()
            };

            _tags.Add(newTag);

            return Task.FromResult(new Tag
            {
                Id = newTag.Id,
                Name = newTag.Name
            });
        }
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
    public Task<bool> DeleteTagAsync(Guid id)
    {
        lock (_lock)
        {
            var tag = _tags.FirstOrDefault(t => t.Id == id);
            if (tag == null)
            {
                return Task.FromResult(false);
            }

            // Удаляем все связи с заметками
            _noteTags.RemoveAll(nt => nt.TagId == id);
            _tags.Remove(tag);

            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<IEnumerable<Tag>> GetTagsForNoteAsync(Guid noteId)
    {
        lock (_lock)
        {
            var tagIds = _noteTags
                .Where(nt => nt.NoteId == noteId)
                .Select(nt => nt.TagId)
                .ToHashSet();

            var tags = _tags
                .Where(t => tagIds.Contains(t.Id))
                .Select(t => new Tag
                {
                    Id = t.Id,
                    Name = t.Name
                })
                .ToList();

            return Task.FromResult<IEnumerable<Tag>>(tags);
        }
    }

    /// <inheritdoc />
    public Task AddTagToNoteAsync(Guid noteId, Guid tagId)
    {
        lock (_lock)
        {
            var existingLink = _noteTags
                .FirstOrDefault(nt => nt.NoteId == noteId && nt.TagId == tagId);

            if (existingLink != null)
            {
                return Task.CompletedTask; // Связь уже существует
            }

            _noteTags.Add(new NoteTag
            {
                NoteId = noteId,
                TagId = tagId
            });

            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public Task RemoveTagFromNoteAsync(Guid noteId, Guid tagId)
    {
        lock (_lock)
        {
            var noteTag = _noteTags
                .FirstOrDefault(nt => nt.NoteId == noteId && nt.TagId == tagId);

            if (noteTag != null)
            {
                _noteTags.Remove(noteTag);
            }

            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Note>> GetNotesWithTagAsync(Guid tagId)
    {
        List<Guid> noteIds;
        lock (_lock)
        {
            noteIds = _noteTags
                .Where(nt => nt.TagId == tagId)
                .Select(nt => nt.NoteId)
                .ToList();
        }

        var allNotes = await _guestNoteService.GetAllNoteAsync();
        return allNotes.Where(n => noteIds.Contains(n.Id)).ToList();
    }

    /// <inheritdoc />
    public async Task<List<(Guid NoteId, Flashcard Card)>> GetFlashcardsByTagsAsync(List<Guid> tagIds)
    {
        if (tagIds == null || tagIds.Count == 0)
        {
            return new List<(Guid, Flashcard)>();
        }

        List<Guid> noteIds;
        lock (_lock)
        {
            // Получаем заметки, у которых есть хотя бы один из указанных тегов
            noteIds = _noteTags
                .Where(nt => tagIds.Contains(nt.TagId))
                .Select(nt => nt.NoteId)
                .Distinct()
                .ToList();
        }

        var allNotes = await _guestNoteService.GetAllNoteAsync();
        var notes = allNotes.Where(n => noteIds.Contains(n.Id)).ToList();

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

    /// <summary>
    /// Сбрасывает гостевое хранилище тегов к начальному состоянию.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _tags.Clear();
            _noteTags.Clear();
        }
    }
}
