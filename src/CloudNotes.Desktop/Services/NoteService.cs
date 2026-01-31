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
    private readonly bool _shouldDisposeContext;

    public NoteService(AppDbContext context, IAuthService? authService = null)
    {
        // Проверяем, является ли контекст InMemory (для тестов)
        // Если да, используем его напрямую, иначе используем DbContextProvider
        var isInMemory = context.Database.ProviderName?.Contains("InMemory") == true;

        if (isInMemory)
        {
            // Для InMemory базы используем переданный контекст напрямую
            // (в тестах lifecycle управляется извне)
            var capturedContext = context;
            _contextFactory = () => capturedContext;
            _shouldDisposeContext = false;
        }
        else
        {
            // Для SQLite создаем новые контексты через DbContextProvider
            _contextFactory = CloudNotes.Services.DbContextProvider.CreateContext;
            _shouldDisposeContext = true;
        }
        _authService = authService;
    }

    public NoteService(Func<AppDbContext> contextFactory, IAuthService? authService = null)
    {
        _contextFactory = contextFactory;
        _authService = authService;
        _shouldDisposeContext = true;
    }

    private AppDbContext CreateContext() => _contextFactory();

    public async Task<Note> CreateNoteAsync(Note note)
    {
        var context = CreateContext();
        if (_shouldDisposeContext)
        {
            using (context)
            {
                return await CreateNoteInternalAsync(context, note);
            }
        }
        return await CreateNoteInternalAsync(context, note);
    }

    private async Task<Note> CreateNoteInternalAsync(AppDbContext context, Note note)
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

        context.Notes.Add(note);

        await context.SaveChangesAsync();

        return note;
    }

    public async Task<IEnumerable<Note>> GetAllNoteAsync()
    {
        var context = CreateContext();
        if (_shouldDisposeContext)
        {
            using (context)
            {
                return await GetAllNoteInternalAsync(context);
            }
        }
        return await GetAllNoteInternalAsync(context);
    }

    private async Task<IEnumerable<Note>> GetAllNoteInternalAsync(AppDbContext context)
    {

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
        var context = CreateContext();
        if (_shouldDisposeContext)
        {
            using (context)
            {
                return await context.Notes.FindAsync(id);
            }
        }
        return await context.Notes.FindAsync(id);
    }

    public async Task<bool> UpdateNoteAsync(Note note, bool fromSync = false)
    {
        var context = CreateContext();
        if (_shouldDisposeContext)
        {
            using (context)
            {
                return await UpdateNoteInternalAsync(context, note, fromSync);
            }
        }
        return await UpdateNoteInternalAsync(context, note, fromSync);
    }

    private async Task<bool> UpdateNoteInternalAsync(AppDbContext context, Note note, bool fromSync)
    {
        Console.WriteLine($"[NoteService] UpdateNoteInternalAsync called: noteId={note.Id}, fromSync={fromSync}");

        var existingNote = await context.Notes.FindAsync(note.Id);
        if (existingNote == null)
        {
            Console.WriteLine($"[NoteService] Note {note.Id} not found in DB!");
            return false;
        }

        var hasServerId = existingNote.ServerId.HasValue;
        Console.WriteLine($"[NoteService] Found note '{existingNote.Title}', hasServerId={hasServerId}, existingIsSynced={existingNote.IsSynced}");

        existingNote.Title = note.Title;
        existingNote.Content = note.Content;
        existingNote.IsFavorite = note.IsFavorite;
        existingNote.ServerId = note.ServerId;
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

        // Логика установки IsSynced:
        // - fromSync = true: вызов от SyncService, используем IsSynced из note (обычно true)
        // - fromSync = false: локальное изменение, если заметка уже на сервере - помечаем как несинхронизированную
        if (fromSync)
        {
            // Вызов от SyncService - используем значение из note
            existingNote.IsSynced = note.IsSynced;
            Console.WriteLine($"[NoteService] fromSync=true, setting IsSynced={note.IsSynced}");
        }
        else if (hasServerId)
        {
            // Локальное изменение синхронизированной заметки - помечаем как несинхронизированную
            existingNote.IsSynced = false;
            Console.WriteLine($"[NoteService] Local change detected, setting IsSynced=false");
        }
        else
        {
            // Заметка ещё не на сервере - используем значение из note
            existingNote.IsSynced = note.IsSynced;
            Console.WriteLine($"[NoteService] No ServerId, keeping IsSynced={note.IsSynced}");
        }

        // Явно помечаем сущность как измененную для гарантии обновления UpdatedAt
        context.Entry(existingNote).State = EntityState.Modified;

        // Save changes (UpdatedAt обновится автоматически в SaveChangesAsync)
        await context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteNoteAsync(Guid id)
    {
        var context = CreateContext();
        if (_shouldDisposeContext)
        {
            using (context)
            {
                return await DeleteNoteInternalAsync(context, id);
            }
        }
        return await DeleteNoteInternalAsync(context, id);
    }

    private async Task<bool> DeleteNoteInternalAsync(AppDbContext context, Guid id)
    {
        var note = await context.Notes.FindAsync(id);
        if (note == null)
        {
            return false;
        }

        // Если заметка была синхронизирована с сервером, сохраняем её ServerId для последующего удаления на сервере
        if (note.ServerId.HasValue && !string.IsNullOrEmpty(note.UserEmail))
        {
            // Проверяем, не была ли эта заметка уже добавлена в список удалённых
            var existingDeleted = await context.DeletedNotes
                .FirstOrDefaultAsync(dn => dn.ServerId == note.ServerId.Value && dn.UserEmail == note.UserEmail);

            if (existingDeleted == null)
            {
                var deletedNote = new Model.DeletedNote
                {
                    Id = Guid.NewGuid(),
                    ServerId = note.ServerId.Value,
                    UserEmail = note.UserEmail,
                    DeletedAt = DateTime.UtcNow
                };
                context.DeletedNotes.Add(deletedNote);
                System.Diagnostics.Debug.WriteLine($"[NoteService] Saved deleted note {note.ServerId.Value} for sync");
            }
        }

        context.Notes.Remove(note);

        await context.SaveChangesAsync();

        return true;
    }
}
