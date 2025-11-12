using System;

namespace CloudNotes.Desktop.Model
{
    public class NoteListItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}