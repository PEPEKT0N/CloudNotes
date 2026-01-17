using System;
using System.Threading.Tasks;
using CloudNotes.Desktop.Data;
using CloudNotes.Services;
using Microsoft.EntityFrameworkCore;

namespace CloudNotes.Desktop.Services;

/// <summary>
/// Фабрика для переключения между гостевым и авторизованным режимами работы с заметками.
/// </summary>
public class NoteServiceFactory : INoteServiceFactory
{
    private readonly GuestNoteService _guestNoteService;
    private readonly GuestTagService _guestTagService;
    private readonly INoteService _authenticatedNoteService;
    private readonly ITagService _authenticatedTagService;
    private readonly AppDbContext _dbContext;
    private bool _isGuestMode = true;

    public event Action<bool>? ModeChanged;

    public NoteServiceFactory(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _guestNoteService = new GuestNoteService();
        _guestTagService = new GuestTagService(_guestNoteService);
        _authenticatedNoteService = new NoteService(dbContext);
        _authenticatedTagService = new TagService(dbContext);
    }

    public INoteService CurrentNoteService => _isGuestMode ? _guestNoteService : _authenticatedNoteService;

    public ITagService CurrentTagService => _isGuestMode ? _guestTagService : _authenticatedTagService;

    public GuestNoteService GuestNoteService => _guestNoteService;

    public GuestTagService GuestTagService => _guestTagService;

    public INoteService AuthenticatedNoteService => _authenticatedNoteService;

    public ITagService AuthenticatedTagService => _authenticatedTagService;

    public bool IsGuestMode => _isGuestMode;

    public void SwitchToGuestMode()
    {
        if (_isGuestMode) return;

        _isGuestMode = true;
        // Сбрасываем гостевое хранилище к начальному состоянию
        _guestNoteService.Reset();
        _guestTagService.Reset();
        ModeChanged?.Invoke(true);

        System.Diagnostics.Debug.WriteLine("NoteServiceFactory: Switched to Guest Mode");
    }

    public void SwitchToAuthenticatedMode()
    {
        if (!_isGuestMode) return;

        _isGuestMode = false;
        // Очищаем гостевое хранилище при переходе в авторизованный режим
        _guestNoteService.Clear();
        _guestTagService.Reset();
        ModeChanged?.Invoke(false);

        System.Diagnostics.Debug.WriteLine("NoteServiceFactory: Switched to Authenticated Mode");
    }

    public async Task ClearLocalDatabaseAsync()
    {
        try
        {
            // Удаляем все связи Note-Tag
            await _dbContext.NoteTags.ExecuteDeleteAsync();

            // Удаляем все теги
            await _dbContext.Tags.ExecuteDeleteAsync();

            // Удаляем все заметки
            await _dbContext.Notes.ExecuteDeleteAsync();

            System.Diagnostics.Debug.WriteLine("NoteServiceFactory: Local database cleared successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NoteServiceFactory: Error clearing local database: {ex.Message}");
            throw;
        }
    }
}
