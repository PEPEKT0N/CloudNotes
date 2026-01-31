
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using CloudNotes.Desktop.Model;

namespace CloudNotes.Desktop.Services
{
    public interface INoteService
    {
        Task<Note> CreateNoteAsync(Note note);
        Task<IEnumerable<Note>> GetAllNoteAsync();
        Task<Note?> GetNoteByIdAsync(Guid id);
        /// <summary>
        /// Обновляет заметку в хранилище.
        /// </summary>
        /// <param name="note">Заметка для обновления</param>
        /// <param name="fromSync">Если true, вызов идёт от SyncService и IsSynced берётся из note. 
        /// Если false, это локальное изменение и IsSynced сбрасывается в false для синхронизированных заметок.</param>
        Task<bool> UpdateNoteAsync(Note note, bool fromSync = false);
        Task<bool> DeleteNoteAsync(Guid id);
    }
}
