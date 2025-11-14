
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
        Task<bool> UpdateNoteAsync(Note note);
        Task<bool> DeleteNoteAsync(Guid id);
    }
}
