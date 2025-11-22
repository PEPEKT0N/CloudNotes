using System;
using System.ComponentModel;

namespace CloudNotes.Desktop.Model
{
    public class NoteListItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public Guid Id { get; set; }
        private string title = string.Empty;
        public string Title
        {
            get => title;
            set
            {
                if (title != value)
                {
                    title = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
                }
            }
        }
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
