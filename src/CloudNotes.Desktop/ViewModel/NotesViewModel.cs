using System;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using System.Linq;
using System.Threading.Tasks;
using CloudNotes.Desktop.Model;
using CloudNotes.Desktop.Services;

namespace CloudNotes.Desktop.ViewModel
{
    public class NotesViewModel : INotifyPropertyChanged
    {
        private readonly INoteService _noteService;

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
                    (RemoveFromFavoritesCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
                    // --- этот вызв заменяет UpdateSelectedNote ---
                    _ = LoadSelectedNoteFromDatabaseAsync(value);
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
        public ICommand RemoveFromFavoritesCommand { get; }
        public ICommand RenameNoteCommand { get; }
        public ICommand DeleteNoteCommand { get; }

        public NotesViewModel(INoteService noteService)
        {
            _noteService = noteService ?? throw new ArgumentNullException(nameof(noteService));

            CreateNoteCommand = new RelayCommand(_ => CreateNote());
            AddToFavoritesCommand = new RelayCommand(_ => AddToFavorites(), _ => CanModifyNote());
            RenameNoteCommand = new RelayCommand(async _ => await RenameNoteAsync(), _ => CanModifyNote());
            DeleteNoteCommand = new RelayCommand(_ => DeleteNote(), _ => CanModifyNote());
            RemoveFromFavoritesCommand = new RelayCommand(_ => RemoveFromFavorites(), _ => SelectedFavoriteItem != null);

            // --- ИЗМЕНЕНО: Заменено AddDefaultNote() на LoadNotesFromDatabaseAsync() ---
            _ = LoadNotesFromDatabaseAsync();
        }

        // --- ИЗМЕНЕНО: Метод для загрузки заметок из БД ---
        private async Task LoadNotesFromDatabaseAsync()
        {
            try
            {
                var allNotesInDb = await _noteService.GetAllNoteAsync();
                var welcomeNoteExists = allNotesInDb.Any(n => n.Title == "Welcome note");
                var secondNoteExists = allNotesInDb.Any(n => n.Title == "Second note");

                // Если одной из дефолтных заметок нет, создаём её
                if (!welcomeNoteExists)
                {
                    var welcomeNote = new Note
                    {
                        Id = Guid.NewGuid(),
                        Title = "Welcome note",
                        Content = "This is a sample note. You can edit it",
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _noteService.CreateNoteAsync(welcomeNote);
                }

                if (!secondNoteExists)
                {
                    var secondNote = new Note
                    {
                        Id = Guid.NewGuid(),
                        Title = "Second note",
                        Content = "Another sample note to test selection.",
                        UpdatedAt = DateTime.UtcNow.AddHours(-1)
                    };
                    await _noteService.CreateNoteAsync(secondNote);
                }

                //загружаем все заметки из БД
                allNotesInDb = await _noteService.GetAllNoteAsync();

                // Очищаем текущие коллекции
                Notes.Clear();
                AllNotes.Clear();
                Favorites.Clear();

                foreach (var note in allNotesInDb)
                {
                    AllNotes.Add(note);
                    Notes.Add(GenerateListItem(note));
                    // Если заметка в избранном, добавляем в Favorites (если используется)
                    // if (note.IsFavorite) // Предположим, у Note есть поле IsFavorite
                    // {
                    //     Favorites.Add(GenerateListItem(note));
                    // }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading notes from database: {ex.Message}");
            }
        }

        // --- ИЗМЕНЕНО: Метод для загрузки SelectedNote из БД ---
        private async Task LoadSelectedNoteFromDatabaseAsync(NoteListItem? listItem)
        {
            if (listItem == null)
            {
                SelectedNote = null;
                return;
            }

            try
            {
                // Загружаем полную заметку из БД по ID через NoteService
                var fullNote = await _noteService.GetNoteByIdAsync(listItem.Id);
                SelectedNote = fullNote;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading note details from database: {ex.Message}");
                SelectedNote = null;
            }
        }

        private bool CanModifyNote()
        {
            return SelectedListItem != null;
        }


        private void AddNote(Note note)
        {
            AllNotes.Add(note);
            Notes.Add(GenerateListItem(note));
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
            if (SelectedListItem == null || SelectedNote == null)
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

            textBox.AttachedToVisualTree += (_, __) =>
            {
                textBox.Focus();
                textBox.SelectAll();
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
                tcs.TrySetResult(textBox.Text);
                dialog.Close();
            };

            dialog.KeyDown += (_, e_) =>
            {
                if (e_.Key == Avalonia.Input.Key.Enter)
                {
                    tcs.TrySetResult(textBox.Text);
                    dialog.Close();
                }
                else if (e_.Key == Avalonia.Input.Key.Escape)
                {
                    tcs.TrySetResult(null);
                    dialog.Close();
                }
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

                var favorite = Favorites.FirstOrDefault(f => f.Id == SelectedListItem.Id);
                if (favorite != null)
                {
                    favorite.Title = newTitle;
                }
            }
        } // RenameNote

        // --- ИЗМЕНЕНО: Метод DeleteNote теперь асинхронный ---
        private void DeleteNote()
        {
            _ = DeleteNoteAsync();
        }

        private async Task DeleteNoteAsync()
        {
            if (SelectedListItem == null)
            {
                return;
            }

            var noteToRemove = SelectedListItem;

            try
            {
                // --- ИЗМЕНЕНО: Удаление через NoteService ---
                var success = await _noteService.DeleteNoteAsync(noteToRemove.Id);
                if (success)
                {
                    // Только если удаление из БД прошло успешно, удаляем из локальных коллекций
                    Notes.Remove(noteToRemove);

                    var note = AllNotes.FirstOrDefault(n => n.Id == noteToRemove.Id);
                    if (note != null)
                    {
                        AllNotes.Remove(note);
                    }
                    var favorite = Favorites.FirstOrDefault(f => f.Id == noteToRemove.Id);
                    if (favorite != null)
                    {
                        Favorites.Remove(favorite);
                    }

                    if (SelectedNote != null && SelectedNote.Id == noteToRemove.Id)
                    {
                        SelectedNote = null;
                    }
                }
                else
                {
                    Console.WriteLine($"Note with Id {noteToRemove.Id} was not found in database during deletion.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting note from database: {ex.Message}");
            }
        }

        private void RemoveFromFavorites()
        {
            if (SelectedFavoriteItem == null)
            {
                return;
            }

            var note = AllNotes.FirstOrDefault(n => n.Id == SelectedFavoriteItem.Id);
            if (note != null)
            {
                note.IsFavorite = false;
            }

            Favorites.Remove(SelectedFavoriteItem);

            SelectedFavoriteItem = null;
        }


        // --- ИЗМЕНЕНО: Метод CreateNote теперь асинхронный ---
        public void CreateNote()
        {
            _ = CreateNoteAsync();
        }

        private async Task CreateNoteAsync()
        {
            try
            {
                var newNote = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Unnamed",
                    Content = "",
                    UpdatedAt = DateTime.UtcNow
                };

                var createdNote = await _noteService.CreateNoteAsync(newNote);

                // Добавляем в локальные коллекции (AllNotes, Notes)
                AllNotes.Add(createdNote);
                var newListItem = GenerateListItem(createdNote);
                Notes.Add(newListItem);

                // Выбираем новую заметку
                SelectedListItem = newListItem;
                SelectedNote = createdNote;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating note in database: {ex.Message}");
            }
        }

        // --- ИЗМЕНЕНО: Метод OnNoteSelected теперь использует LoadSelectedNoteFromDatabaseAsync ---
        public void OnNoteSelected(NoteListItem? listItem)
        {
            // Теперь вызывается через SelectedListItem setter
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
