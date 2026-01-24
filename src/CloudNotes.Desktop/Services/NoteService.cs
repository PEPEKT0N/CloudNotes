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
    private readonly Func<AppDbContext> _contextFactory;
    private readonly IAuthService? _authService;

    public NoteService(AppDbContext context, IAuthService? authService = null)
    {
        // Используем фабрику для создания нового контекста для каждой операции
        // Это обеспечивает thread-safety
        _contextFactory = CloudNotes.Services.DbContextProvider.CreateContext;
        _authService = authService;
    }

    public NoteService(Func<AppDbContext> contextFactory, IAuthService? authService = null)
    {
        _contextFactory = contextFactory;
        _authService = authService;
    }

    private AppDbContext CreateContext() => _contextFactory();

    public async Task<Note> CreateNoteAsync(Note note)
    {
        using var context = CreateContext();
        
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

        context.Notes.Add(note);

        await context.SaveChangesAsync();

        return note;
    }

    public async Task<IEnumerable<Note>> GetAllNoteAsync()
    {
        using var context = CreateContext();
        
        // Фильтруем заметки по текущему пользователю
        var query = context.Notes.AsQueryable();

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
        using var context = CreateContext();
        return await context.Notes.FindAsync(id);
    }

    public async Task<bool> UpdateNoteAsync(Note note)
    {
        using var context = CreateContext();
        
        var existingNote = await context.Notes.FindAsync(note.Id);
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
        context.Entry(existingNote).State = EntityState.Modified;

        // Save changes (UpdatedAt обновится автоматически в SaveChangesAsync)
        await context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteNoteAsync(Guid id)
    {
        using var context = CreateContext();
        
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
