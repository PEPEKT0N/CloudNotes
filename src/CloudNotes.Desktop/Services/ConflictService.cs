using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudNotes.Desktop.Api;
using CloudNotes.Desktop.Model;
using CloudNotes.Services;

namespace CloudNotes.Desktop.Services;

// Сервис для управления конфликтами заметок
public class ConflictService : IConflictService
{
    private readonly INoteService _noteService;
    private readonly List<NoteConflict> _conflicts = new();
    private readonly object _lock = new();

    public event Action<NoteConflict>? ConflictDetected;

    public ConflictService(INoteService noteService)
    {
        _noteService = noteService ?? throw new ArgumentNullException(nameof(noteService));
    }

    // Добавить конфликт
    public void AddConflict(NoteConflict conflict)
    {
        if (conflict == null)
            throw new ArgumentNullException(nameof(conflict));

        lock (_lock)
        {
            // Удаляем существующий конфликт для этой заметки, если есть
            _conflicts.RemoveAll(c => c.LocalNoteId == conflict.LocalNoteId);
            _conflicts.Add(conflict);
        }

        ConflictDetected?.Invoke(conflict);
    }

    // Получить все конфликты
    public IReadOnlyList<NoteConflict> GetConflicts()
    {
        lock (_lock)
        {
            return _conflicts.ToList().AsReadOnly();
        }
    }

    // Разрешить конфликт (выбрать локальную или серверную версию)
    public async Task<bool> ResolveConflictAsync(Guid localNoteId, bool useServerVersion)
    {
        NoteConflict? conflict;
        lock (_lock)
        {
            conflict = _conflicts.FirstOrDefault(c => c.LocalNoteId == localNoteId);
            if (conflict == null)
            {
                return false;
            }
        }

        var localNote = await _noteService.GetNoteByIdAsync(localNoteId);
        if (localNote == null)
        {
            RemoveConflict(localNoteId);
            return false;
        }

        if (useServerVersion)
        {
            // Используем серверную версию
            localNote.Title = conflict.ServerNote.Title;
            localNote.Content = conflict.ServerNote.Content ?? string.Empty;
            localNote.UpdatedAt = conflict.ServerNote.UpdatedAt;
            localNote.ServerId = conflict.ServerNote.Id;
            localNote.IsSynced = true;
        }
        else
        {
            // Используем локальную версию - помечаем как несинхронизированную для повторной отправки
            localNote.IsSynced = false;
        }

        await _noteService.UpdateNoteAsync(localNote);
        RemoveConflict(localNoteId);

        return true;
    }

    // Удалить конфликт
    public void RemoveConflict(Guid localNoteId)
    {
        lock (_lock)
        {
            _conflicts.RemoveAll(c => c.LocalNoteId == localNoteId);
        }
    }
}

