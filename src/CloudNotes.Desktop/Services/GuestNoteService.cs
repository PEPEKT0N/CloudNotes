using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudNotes.Desktop.Model;

namespace CloudNotes.Desktop.Services;

/// <summary>
/// InMemory реализация INoteService для гостевого режима.
/// Заметки хранятся только в памяти и не сохраняются в БД.
/// </summary>
public class GuestNoteService : INoteService
{
    private readonly List<Note> _notes = new();
    private readonly object _lock = new();
    private bool _initialized = false;

    public GuestNoteService()
    {
        InitializeDefaultNotes();
    }

    /// <summary>
    /// Инициализирует дефолтные заметки для гостевого режима.
    /// </summary>
    private void InitializeDefaultNotes()
    {
        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;

            var now = DateTime.Now;

            _notes.Add(new Note
            {
                Id = Guid.NewGuid(),
                Title = "Welcome note",
                Content = "This is a sample note. You can edit it.",
                CreatedAt = now,
                UpdatedAt = now,
                IsFavorite = false,
                IsSynced = false
            });

            _notes.Add(new Note
            {
                Id = Guid.NewGuid(),
                Title = "Second note",
                Content = "Another sample note to test selection.",
                CreatedAt = now,
                UpdatedAt = now,
                IsFavorite = false,
                IsSynced = false
            });

            _initialized = true;
        }
    }

    public Task<Note> CreateNoteAsync(Note note)
    {
        lock (_lock)
        {
            // Клонируем заметку, чтобы избежать проблем с shared references
            var newNote = new Note
            {
                Id = note.Id == Guid.Empty ? Guid.NewGuid() : note.Id,
                Title = note.Title,
                Content = note.Content,
                CreatedAt = note.CreatedAt == default ? DateTime.Now : note.CreatedAt,
                UpdatedAt = DateTime.Now,
                IsFavorite = note.IsFavorite,
                IsSynced = false, // Гостевые заметки никогда не синхронизируются
                ServerId = null
            };

            _notes.Add(newNote);
            return Task.FromResult(newNote);
        }
    }

    public Task<IEnumerable<Note>> GetAllNoteAsync()
    {
        lock (_lock)
        {
            // Возвращаем копии заметок, чтобы изменения в UI не затрагивали хранилище напрямую
            var notesCopy = _notes.Select(n => new Note
            {
                Id = n.Id,
                Title = n.Title,
                Content = n.Content,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt,
                IsFavorite = n.IsFavorite,
                IsSynced = n.IsSynced,
                ServerId = n.ServerId
            }).ToList();

            return Task.FromResult<IEnumerable<Note>>(notesCopy);
        }
    }

    public Task<Note?> GetNoteByIdAsync(Guid id)
    {
        lock (_lock)
        {
            var note = _notes.FirstOrDefault(n => n.Id == id);
            if (note == null) return Task.FromResult<Note?>(null);

            // Возвращаем копию
            return Task.FromResult<Note?>(new Note
            {
                Id = note.Id,
                Title = note.Title,
                Content = note.Content,
                CreatedAt = note.CreatedAt,
                UpdatedAt = note.UpdatedAt,
                IsFavorite = note.IsFavorite,
                IsSynced = note.IsSynced,
                ServerId = note.ServerId
            });
        }
    }

    public Task<bool> UpdateNoteAsync(Note note)
    {
        lock (_lock)
        {
            var existingNote = _notes.FirstOrDefault(n => n.Id == note.Id);
            if (existingNote == null)
            {
                return Task.FromResult(false);
            }

            existingNote.Title = note.Title;
            existingNote.Content = note.Content;
            existingNote.IsFavorite = note.IsFavorite;
            existingNote.UpdatedAt = DateTime.Now;
            // Гостевые заметки никогда не синхронизируются
            existingNote.IsSynced = false;
            existingNote.ServerId = null;

            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteNoteAsync(Guid id)
    {
        lock (_lock)
        {
            var note = _notes.FirstOrDefault(n => n.Id == id);
            if (note == null)
            {
                return Task.FromResult(false);
            }

            _notes.Remove(note);
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Сбрасывает гостевое хранилище к начальному состоянию.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _notes.Clear();
            _initialized = false;
            InitializeDefaultNotes();
        }
    }

    /// <summary>
    /// Очищает все заметки из гостевого хранилища.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _notes.Clear();
            _initialized = true; // Помечаем как инициализированный, чтобы не создавать дефолтные заметки
        }
    }
}
