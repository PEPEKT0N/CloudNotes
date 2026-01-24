using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudNotes.Desktop.Data;
using CloudNotes.Desktop.Model;
using CloudNotes.Services; // Убедись, что это пространство имён для INoteService
using Microsoft.EntityFrameworkCore;

namespace CloudNotes.Desktop.Services;

public class NoteService : INoteService
{
    private readonly AppDbContext _context;
    private readonly IAuthService? _authService;

    public NoteService(AppDbContext context, IAuthService? authService = null)
    {
        _context = context;
        _authService = authService;
    }

    public async Task<Note> CreateNoteAsync(Note note)
    {
        // Новая заметка создается локально - помечаем как несинхронизированную
        if (!note.ServerId.HasValue)
        {
            note.IsSynced = false;
        }

        // Устанавливаем UserEmail для текущего пользователя
        if (_authService != null)
        {
            var currentEmail = await _authService.GetCurrentUserEmailAsync();
            note.UserEmail = currentEmail;
        }

        _context.Notes.Add(note);

        await _context.SaveChangesAsync();

        return note;
    }

    public async Task<IEnumerable<Note>> GetAllNoteAsync()
    {
        // Фильтруем заметки по текущему пользователю
        var query = _context.Notes.AsQueryable();

        if (_authService != null)
        {
            var currentEmail = await _authService.GetCurrentUserEmailAsync();
            if (currentEmail != null)
            {
                // Показываем только заметки текущего пользователя
                query = query.Where(n => n.UserEmail == currentEmail);
            }
            else
            {
                // Если пользователь не авторизован, не показываем заметки с UserEmail
                query = query.Where(n => n.UserEmail == null);
            }
        }
        else
        {
            // Если AuthService недоступен, показываем только заметки без UserEmail (гостевые)
            query = query.Where(n => n.UserEmail == null);
        }

        return await query.ToListAsync();
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

        var wasSynced = existingNote.IsSynced;

        existingNote.Title = note.Title;
        existingNote.Content = note.Content;
        existingNote.IsFavorite = note.IsFavorite;
        existingNote.ServerId = note.ServerId;
        existingNote.IsSynced = note.IsSynced;
        existingNote.FolderId = note.FolderId;
        
        // Обновляем UserEmail если он был передан
        if (note.UserEmail != null)
        {
            existingNote.UserEmail = note.UserEmail;
        }
        else if (_authService != null)
        {
            // Если UserEmail не передан, получаем его из AuthService
            var currentEmail = await _authService.GetCurrentUserEmailAsync();
            existingNote.UserEmail = currentEmail;
        }

        // Если заметка была синхронизирована и мы обновляем её локально (не из синхронизации),
        // то помечаем как несинхронизированную для повторной синхронизации
        // SyncService явно устанавливает IsSynced = true, поэтому это не затронет синхронизацию
        if (wasSynced && !note.IsSynced)
        {
            existingNote.IsSynced = false;
        }

        // Явно помечаем сущность как измененную для гарантии обновления UpdatedAt
        _context.Entry(existingNote).State = EntityState.Modified;

        // Save changes (UpdatedAt обновится автоматически в SaveChangesAsync)
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
