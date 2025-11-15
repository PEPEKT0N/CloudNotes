using System;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using CloudNotes.Desktop.Model;


namespace CloudNotes.Desktop.ViewModel
{
    public class NotesViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public ObservableCollection<NoteListItem> Notes { get; } = new();
        public List<Note> AllNotes { get; } = new();

        private NoteListItem? selectedListItem;
        public NoteListItem? SelectedListItem
        {
            get { return selectedListItem; }
            set
            {
                if (selectedListItem != value)
                {
                    selectedListItem = value;
                    OnPropertyChanged();
                    UpdateSelectedNote(value);
                }
            }
        }
        private Note? selectedNote;
        public Note? SelectedNote
        {
            get { return selectedNote; }
            set
            {
                if (selectedNote != value)
                {
                    selectedNote = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand CreateNoteCommand { get; }

        public NotesViewModel()
        {
            CreateNoteCommand = new RelayCommand(_ => CreateNote());
            AddDefaultNote();
        }
        private void AddDefaultNote()
        {
            AddNote(new Note
            {
                Id = Guid.NewGuid(),
                Title = "Welcome note",
                Content = "This is a sample note. You can edit it",
                UpdatedAt = DateTime.Now
            });

            AddNote(new Note
            {
                Id = Guid.NewGuid(),
                Title = "Second note",
                Content = "Another sample note to test selection.",
                UpdatedAt = DateTime.Now.AddHours(-1)
            });

            SelectedListItem = null;
            SelectedNote = null;
        }
        private void AddNote(Note note)
        {
            AllNotes.Add(note);
            Notes.Add(GenerateListItem(note));
            //SelectedListItem = Notes[^1];
        }

        private void UpdateSelectedNote(NoteListItem? listItem)
        {
            if (listItem == null)
            {
                SelectedNote = null;
                return;
            }
            SelectedNote = AllNotes.Find(n => n.Id == listItem.Id);
        }

        public void CreateNote()
        {
            var newNote = new Note
            {
                Id = Guid.NewGuid(),
                Title = "Unnamed",
                Content = "",
                UpdatedAt = DateTime.Now
            };

            AllNotes.Add(newNote);

            var newListItem = GenerateListItem(newNote);

            Notes.Add(newListItem);
            SelectedListItem = newListItem;
            SelectedNote = newNote;
        }

        public void OnNoteSelected(NoteListItem? listItem)
        {
            if (listItem == null)
            {
                SelectedNote = null;
                return;
            }

            SelectedNote = AllNotes.Find(n => n.Id == listItem.Id);
        }

        private NoteListItem GenerateListItem(Note note)
        {
            return new NoteListItem
            {
                Id = note.Id,
                Title = note.Title,
                UpdatedAt = note.UpdatedAt
            };
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
