using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CloudNotes.Desktop.Data;
using CloudNotes.Desktop.Model;
using Microsoft.EntityFrameworkCore;

namespace CloudNotes.Desktop.Services;

public class NoteService : INoteService
{
    private readonly AppDbContext _context;

    public NoteService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Note> CreateNoteAsync(Note note)
    {
        _context.Notes.Add(note);

        await _context.SaveChangesAsync();

        return note;
    }

    public async Task<IEnumerable<Note>> GetAllNoteAsync()
    {
        return await _context.Notes.ToListAsync();
    }

    public async Task<Note?> GetNoteByIdAsync(Guid id)
    {
        return await _context.Notes.FindAsync(id);
    }

    public async Task<bool> UpdateNoteAsync(Note note)
    {
        //Check if the note exists
        var existingNote = await _context.Notes.FindAsync(note.Id);
        if (existingNote == null)
        {
            return false;
        }

        //Update the note
        existingNote.Title = note.Title;
        existingNote.Content = note.Content;
        existingNote.IsFavorite = note.IsFavorite;

        // Явно помечаем сущность как измененную для гарантии обновления UpdatedAt
        _context.Entry(existingNote).State = EntityState.Modified;

        // Save changes (UpdatedAt обновится автоматически в SaveChangesAsync)
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteNoteAsync(Guid id)
    {
        // Find the note by Id
        var note = await _context.Notes.FindAsync(id);
        if (note == null)
        {
            return false;
        }

        // Remove the note
        _context.Notes.Remove(note);

        await _context.SaveChangesAsync();

        return true;
    }
}
