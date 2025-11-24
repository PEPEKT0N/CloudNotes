using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CloudNotes.Desktop.Model;
using CloudNotes.Services; // INoteService
using Microsoft.EntityFrameworkCore;

namespace CloudNotes.Desktop.Services;

public class NoteService : INoteService
{
    public NoteService()
    {
        
    }

    public async Task<Note> CreateNoteAsync(Note note)
    {
        // Получаем контекст через провайдер
        var context = DbContextProvider.GetContext();
        note.UpdatedAt = DateTime.UtcNow;

        context.Notes.Add(note);

        await context.SaveChangesAsync();

        return note;
    }

    public async Task<IEnumerable<Note>> GetAllNoteAsync()
    {
        var context = DbContextProvider.GetContext();
        return await context.Notes.ToListAsync();
    }

    public async Task<Note?> GetNoteByIdAsync(Guid id)
    {
        var context = DbContextProvider.GetContext();
        return await context.Notes.FindAsync(id);
    }

    public async Task<bool> UpdateNoteAsync(Note note)
    {
        var context = DbContextProvider.GetContext();
        var existingNote = await context.Notes.FindAsync(note.Id);
        if (existingNote == null)
        {
            return false;
        }

        existingNote.Title = note.Title;
        existingNote.Content = note.Content;
        existingNote.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteNoteAsync(Guid id)
    {
        var context = DbContextProvider.GetContext();
        var note = await context.Notes.FindAsync(id);
        if (note == null)
        {
            return false;
        }

        context.Notes.Remove(note);

        await context.SaveChangesAsync();

        return true;
    }
}