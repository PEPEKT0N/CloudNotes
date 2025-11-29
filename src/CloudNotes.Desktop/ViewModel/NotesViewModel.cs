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
using CloudNotes.Services;

namespace CloudNotes.Desktop.ViewModel
{
    public class NotesViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        // UI коллекции — NoteListItem (с INotifyPropertyChanged)
        public ObservableCollection<NoteListItem> Notes { get; } = new();
        public ObservableCollection<NoteListItem> Favorites { get; } = new();

        // Данные — Note (чистая сущность)
        public List<Note> AllNotes { get; } = new();

        // Выбранный элемент в списке избранного
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

        // Выбранный элемент в основном списке
        private NoteListItem? selectedListItem;
        public NoteListItem? SelectedListItem
        {
            get => selectedListItem;
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

        // Текущая выбранная заметка (для редактирования контента)
        private Note? selectedNote;
        public Note? SelectedNote
        {
            get => selectedNote;
            set
            {
                if (selectedNote != value)
                {
                    selectedNote = value;
                    OnPropertyChanged();
                }
            }
        }

        // Активная заметка (для операций через контекстное меню/горячие клавиши)
        private NoteListItem? activeListItem;
        public NoteListItem? ActiveListItem
        {
            get => activeListItem;
            private set
            {
                if (activeListItem != value)
                {
                    activeListItem = value;
                    OnPropertyChanged();
                }
            }
        }

        // Команды
        public ICommand CreateNoteCommand { get; }
        public ICommand AddToFavoritesCommand { get; }
        public ICommand RemoveFromFavoritesCommand { get; }
        public ICommand DeleteNoteCommand { get; }

        // Сервис для работы с БД
        private readonly INoteService _noteService;

        public NotesViewModel()
        {
            var context = DbContextProvider.GetContext();
            _noteService = new NoteService(context);

            CreateNoteCommand = new RelayCommand(_ => CreateNote());
            AddToFavoritesCommand = new RelayCommand(_ => AddToFavorites(), _ => CanModifyNote());
            DeleteNoteCommand = new RelayCommand(_ => DeleteNote(), _ => CanModifyNote());
            RemoveFromFavoritesCommand = new RelayCommand(_ => RemoveFromFavorites(), _ => SelectedFavoriteItem != null);

            // Загружаем заметки из БД асинхронно
            Task.Run(async () => await LoadNotesFromDbAsync());
        }

        private bool CanModifyNote() => SelectedListItem != null;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async Task LoadNotesFromDbAsync()
        {
            // Загружаем заметки из БД
            var notesFromDb = await _noteService.GetAllNoteAsync();

            if (!notesFromDb.Any())
            {
                // Если БД пустая, создаем дефолтные заметки в БД
                await CreateDefaultNotesInDb();
                // После создания загружаем их из БД
                notesFromDb = await _noteService.GetAllNoteAsync();
            }

            // Загружаем все заметки из БД в коллекцию
            foreach (var note in notesFromDb)
            {
                AllNotes.Add(note);
                Notes.Add(CreateListItem(note));

                // Добавляем в избранное, если нужно
                if (note.IsFavorite)
                {
                    Favorites.Add(CreateListItem(note));
                }
            }

            SelectedListItem = null;
            SelectedNote = null;
        }

        private async Task CreateDefaultNotesInDb()
        {
            var note1 = new Note
            {
                Id = Guid.NewGuid(),
                Title = "Welcome note",
                Content = "This is a sample note. You can edit it.",
                UpdatedAt = DateTime.Now
            };

            var note2 = new Note
            {
                Id = Guid.NewGuid(),
                Title = "Second note",
                Content = "Another sample note to test selection.",
                UpdatedAt = DateTime.Now.AddHours(-1)
            };

            // Сохраняем дефолтные заметки только в БД
            await _noteService.CreateNoteAsync(note1);
            await _noteService.CreateNoteAsync(note2);
        }

        public async Task SaveNoteAsync(Note note)
        {
            if (note == null) return;

            note.UpdatedAt = DateTime.Now;
            await _noteService.UpdateNoteAsync(note);
        }

        private void AddNote(Note note)
        {
            AllNotes.Add(note);
            Notes.Add(CreateListItem(note));
        }

        private NoteListItem CreateListItem(Note note)
        {
            return new NoteListItem(note.Id, note.Title, note.UpdatedAt);
        }


        private void UpdateSelectedNote(NoteListItem? listItem)
        {
            if (listItem == null)
            {
                SelectedNote = null;
                ActiveListItem = null;
                return;
            }

            SelectedNote = AllNotes.Find(n => n.Id == listItem.Id);
            ActiveListItem = listItem;
        }

        // -------------------------------------------------------
        // CRUD операции
        // -------------------------------------------------------

        public void CreateNote()
        {
            var note = new Note
            {
                Id = Guid.NewGuid(),
                Title = "Unnamed",
                Content = "",
                UpdatedAt = DateTime.Now
            };

            AllNotes.Add(note);

            var listItem = CreateListItem(note);
            Notes.Add(listItem);

            SelectedListItem = listItem;
            SelectedNote = note;
            ActiveListItem = listItem;

            // Сохраняем в БД асинхронно
            Task.Run(async () => await _noteService.CreateNoteAsync(note));
        }

        public void RenameActiveNote(string newName)
        {
            var listItem = ActiveListItem ?? SelectedListItem;
            if (listItem == null) return;

            // Обновляем UI (NoteListItem уведомит об изменении)
            listItem.Title = newName;

            // Обновляем данные
            var note = AllNotes.FirstOrDefault(n => n.Id == listItem.Id);
            if (note != null)
            {
                note.Title = newName;
                note.UpdatedAt = DateTime.Now;

                // Сохраняем изменения в БД
                Task.Run(async () => await _noteService.UpdateNoteAsync(note));
            }

            // Обновляем в избранном, если есть
            var favoriteItem = Favorites.FirstOrDefault(f => f.Id == listItem.Id);
            if (favoriteItem != null)
            {
                favoriteItem.Title = newName;
            }
        }

        private void AddToFavorites()
        {
            if (SelectedListItem == null) return;

            var note = AllNotes.FirstOrDefault(n => n.Id == SelectedListItem.Id);
            if (note == null) return;

            if (!note.IsFavorite)
            {
                note.IsFavorite = true;
                // Сохраняем изменения в БД
                Task.Run(async () => await _noteService.UpdateNoteAsync(note));
            }

            if (!Favorites.Any(f => f.Id == note.Id))
            {
                Favorites.Add(new NoteListItem(note.Id, note.Title, note.UpdatedAt));
            }
        }

        private void DeleteNote()
        {
            if (SelectedListItem == null) return;

            var listItem = SelectedListItem;
            var noteId = listItem.Id;

            // Удаляем из UI коллекций
            Notes.Remove(listItem);
            var favoriteItem = Favorites.FirstOrDefault(f => f.Id == listItem.Id);
            if (favoriteItem != null)
            {
                Favorites.Remove(favoriteItem);
            }

            // Удаляем из данных
            var note = AllNotes.FirstOrDefault(n => n.Id == listItem.Id);
            if (note != null)
            {
                AllNotes.Remove(note);
            }

            // Удаляем из БД
            Task.Run(async () => await _noteService.DeleteNoteAsync(noteId));

            // Сбрасываем выбор
            if (SelectedNote?.Id == listItem.Id)
            {
                SelectedNote = null;
            }
            if (ActiveListItem?.Id == listItem.Id)
            {
                ActiveListItem = null;
            }

            SelectedListItem = null;
        }

        public void DeleteActiveNote()
        {
            var listItem = ActiveListItem ?? SelectedListItem;
            if (listItem == null) return;

            var noteId = listItem.Id;

            // Удаляем из UI коллекций
            Notes.Remove(listItem);
            var favoriteItem = Favorites.FirstOrDefault(f => f.Id == listItem.Id);
            if (favoriteItem != null)
            {
                Favorites.Remove(favoriteItem);
            }

            // Удаляем из данных
            var note = AllNotes.FirstOrDefault(n => n.Id == listItem.Id);
            if (note != null)
            {
                AllNotes.Remove(note);
            }

            // Удаляем из БД
            Task.Run(async () => await _noteService.DeleteNoteAsync(noteId));

            // Сбрасываем выбор
            SelectedNote = null;
            SelectedListItem = null;
            ActiveListItem = null;
        }

        private void RemoveFromFavorites()
        {
            if (SelectedFavoriteItem == null) return;

            var note = AllNotes.FirstOrDefault(n => n.Id == SelectedFavoriteItem.Id);
            if (note != null)
            {
                note.IsFavorite = false;
                // Сохраняем изменения в БД
                Task.Run(async () => await _noteService.UpdateNoteAsync(note));
            }

            Favorites.Remove(SelectedFavoriteItem);
            SelectedFavoriteItem = null;
        }
    }
}
