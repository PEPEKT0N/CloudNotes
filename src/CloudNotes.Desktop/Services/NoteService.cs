using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CloudNotes.Desktop.Data;
using CloudNotes.Desktop.Model;
using CloudNotes.Services; // Убедись, что это пространство имён для INoteService
using Microsoft.EntityFrameworkCore;

namespace CloudNotes.Desktop.Services;

public class NoteService : INoteService
{
    private readonly AppDbContext _context;

    // Новый конструктор: принимает контекст извне (например, из DI или тестов)
    public NoteService(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    // Старый конструктор: для совместимости с существующим кодом (например, MainWindow)
    // Помечаем как устаревший, чтобы в будущем избавиться от него
    [Obsolete("Use constructor with AppDbContext for better testability and DI.")]
    public NoteService() : this(DbContextProvider.GetContext()) // Вызов нового конструктора
    {
    }

    public async Task<Note> CreateNoteAsync(Note note)
    {
        note.UpdatedAt = DateTime.Now;

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
        var existingNote = await _context.Notes.FindAsync(note.Id);
        if (existingNote == null)
        {
            return false;
        }

        existingNote.Title = note.Title;
        existingNote.Content = note.Content;
        existingNote.UpdatedAt = DateTime.Now;

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteNoteAsync(Guid id)
    {
        var note = await _context.Notes.FindAsync(id);
        if (note == null)
        {
            return false;
        }

        _context.Notes.Remove(note);

        await _context.SaveChangesAsync();

        return true;
    }
}