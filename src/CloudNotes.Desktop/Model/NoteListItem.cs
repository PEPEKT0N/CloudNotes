using System;

namespace CloudNotes.Desktop.Model
{
    public class NoteListItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public NoteListItem(Guid id, string title)
        {
            Id = id;
            Title = title;
        }
        public NoteListItem(Guid id, string title, DateTime updatedAt)
        {
            Id = id;
            Title = title;
            UpdatedAt = updatedAt;
        }
    }
}
