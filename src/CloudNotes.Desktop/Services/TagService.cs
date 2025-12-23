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
    private readonly AppDbContext _context;

    public TagService(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Tag>> GetAllTagsAsync()
    {
        return await _context.Tags.ToListAsync();
    }

    /// <inheritdoc />
    public async Task<Tag?> GetTagByIdAsync(Guid id)
    {
        return await _context.Tags.FindAsync(id);
    }

    /// <inheritdoc />
    public async Task<Tag?> GetTagByNameAsync(string name)
    {
        return await _context.Tags
            .FirstOrDefaultAsync(t => t.Name.ToLower() == name.ToLower());
    }

    /// <inheritdoc />
    public async Task<Tag> CreateTagAsync(Tag tag)
    {
        if (tag.Id == Guid.Empty)
        {
            tag.Id = Guid.NewGuid();
        }

        _context.Tags.Add(tag);
        await _context.SaveChangesAsync();
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
        var tag = await _context.Tags.FindAsync(id);
        if (tag == null)
        {
            return false;
        }

        _context.Tags.Remove(tag);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Tag>> GetTagsForNoteAsync(Guid noteId)
    {
        return await _context.NoteTags
            .Where(nt => nt.NoteId == noteId)
            .Include(nt => nt.Tag)
            .Select(nt => nt.Tag)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task AddTagToNoteAsync(Guid noteId, Guid tagId)
    {
        var existingLink = await _context.NoteTags
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

        _context.NoteTags.Add(noteTag);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task RemoveTagFromNoteAsync(Guid noteId, Guid tagId)
    {
        var noteTag = await _context.NoteTags
            .FirstOrDefaultAsync(nt => nt.NoteId == noteId && nt.TagId == tagId);

        if (noteTag == null)
        {
            return;
        }

        _context.NoteTags.Remove(noteTag);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Note>> GetNotesWithTagAsync(Guid tagId)
    {
        return await _context.NoteTags
            .Where(nt => nt.TagId == tagId)
            .Include(nt => nt.Note)
            .Select(nt => nt.Note)
            .ToListAsync();
    }
}
