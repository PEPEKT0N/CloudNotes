using System;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using CloudNotes.Desktop.Model;
using System.Linq;
using System.Threading.Tasks;


namespace CloudNotes.Desktop.ViewModel
{
    public class NotesViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public ObservableCollection<NoteListItem> Notes { get; } = new();
        public ObservableCollection<NoteListItem> Favorites { get; } = new();
        public List<Note> AllNotes { get; } = new();

        private NoteListItem? selectedFavoriteItem;
        public NoteListItem? SelectedFavoriteItem
        {
            get => selectedFavoriteItem;
            set
            {
                if (selectedFavoriteItem != value)
                {
                    selectedFavoriteItem = value;
                    OnPropertyChanged();
                }
            }
        }
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
        public ICommand AddToFavoritesCommand { get; }
        //public ICommand RemoveFromFavoritesCommand { get; }
        public ICommand RenameNoteCommand { get; }
        public ICommand DeleteNoteCommand { get; }

        public NotesViewModel()
        {
            CreateNoteCommand = new RelayCommand(_ => CreateNote());
            AddToFavoritesCommand = new RelayCommand(_ => AddToFavorites(), _ => CanModifyNote());
            RenameNoteCommand = new RelayCommand(async _ => await RenameNoteAsync(), _ => CanModifyNote());
            DeleteNoteCommand = new RelayCommand(_ => DeleteNote(), _ => CanModifyNote());

            Favorites = new ObservableCollection<NoteListItem>(AllNotes.Where(n => n.IsFavorite).Select(n => new NoteListItem(n.Id, n.Title)));
            AddDefaultNote();

        }
        private bool CanModifyNote()
        {
            return SelectedListItem != null;
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

        private void AddToFavorites()
        {
            if (SelectedListItem == null)
            {
                return;
            }

            var note = AllNotes.FirstOrDefault(n => n.Id == SelectedListItem.Id);
            if (note == null)
            {
                return;
            }

            if (!note.IsFavorite)
            {
                note.IsFavorite = true;
            }

            if (!Favorites.Any(f => f.Id == note.Id))
            {
                Favorites.Add(new NoteListItem(note.Id, note.Title));
            }

            (AddToFavoritesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private async Task RenameNoteAsync()
        {
            if (SelectedListItem == null)
            {
                return;
            }

            var dialog = new Avalonia.Controls.Window
            {
                Width = 300,
                Height = 100,
                Title = "Rename Note"
            };

            var textBox = new Avalonia.Controls.TextBox
            {
                Text = SelectedListItem.Title,
                Margin = new Avalonia.Thickness(10)
            };

            var button = new Avalonia.Controls.Button
            {
                Content = "Rename",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Margin = new Avalonia.Thickness(10)
            };

            var stack = new Avalonia.Controls.StackPanel();
            stack.Children.Add(textBox);
            stack.Children.Add(button);

            dialog.Content = stack;

            var tcs = new TaskCompletionSource<string?>();
            button.Click += (_, __) =>
            {
                tcs.SetResult(textBox.Text);
                dialog.Close();
            };

            dialog.Show();

            var newTitle = await tcs.Task;

            if (!string.IsNullOrWhiteSpace(newTitle))
            {
                SelectedListItem.Title = newTitle;

                var note = AllNotes.FirstOrDefault(n => n.Id == SelectedListItem.Id);
                if (note != null)
                {
                    note.Title = newTitle;
                }
            }
        } // RenameNote

        private void DeleteNote()
        {
            if (SelectedListItem == null)
            {
                return;
            }

            var noteToRemove = SelectedListItem;

            Notes.Remove(noteToRemove);

            var note = AllNotes.FirstOrDefault(n => n.Id == noteToRemove.Id);
            if (note != null)
            {
                AllNotes.Remove(note);
            }

            if (SelectedNote != null && SelectedNote.Id == noteToRemove.Id)
            {
                SelectedNote = null;
            }
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
            return new NoteListItem(note.Id, note.Title, note.UpdatedAt);
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
