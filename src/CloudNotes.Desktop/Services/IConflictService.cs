using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CloudNotes.Desktop.Model;

namespace CloudNotes.Desktop.Services;

// Сервис для управления конфликтами заметок
public interface IConflictService
{
    // Добавить конфликт
    void AddConflict(NoteConflict conflict);

    // Получить все конфликты
    IReadOnlyList<NoteConflict> GetConflicts();

    // Разрешить конфликт (выбрать локальную или серверную версию)
    Task<bool> ResolveConflictAsync(Guid localNoteId, bool useServerVersion);

    // Удалить конфликт
    void RemoveConflict(Guid localNoteId);

    // Событие при обнаружении нового конфликта
    event Action<NoteConflict>? ConflictDetected;
}

