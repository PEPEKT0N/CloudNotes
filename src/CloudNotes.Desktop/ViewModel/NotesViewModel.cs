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
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

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

        // Флаг для предотвращения рекурсии при сбросе выбора между списками
        private bool _isUpdatingSelection = false;

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

                    // При выборе в избранном сбрасываем выбор в основном списке
                    // и обновляем SelectedNote/ActiveListItem
                    if (value != null && !_isUpdatingSelection)
                    {
                        _isUpdatingSelection = true;
                        SelectedListItem = null;
                        _isUpdatingSelection = false;

                        // Обновляем SelectedNote и ActiveListItem
                        SelectedNote = AllNotes.Find(n => n.Id == value.Id);
                        ActiveListItem = value;

                        // Загружаем теги для выбранной заметки
                        Task.Run(async () => await LoadTagsForCurrentNoteAsync());
                    }
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

                    // При выборе в основном списке сбрасываем выбор в избранном
                    if (value != null && !_isUpdatingSelection)
                    {
                        _isUpdatingSelection = true;
                        SelectedFavoriteItem = null;
                        _isUpdatingSelection = false;
                    }

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

                    // Обновляем доступность команд
                    (AddToFavoritesCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (DeleteNoteCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        // Команды
        public ICommand CreateNoteCommand { get; }
        public ICommand AddToFavoritesCommand { get; }
        public ICommand RemoveFromFavoritesCommand { get; }
        public ICommand DeleteNoteCommand { get; }

        // Сортировка
        private SortOption _selectedSortOption = SortOption.TitleAsc;
        public SortOption SelectedSortOption
        {
            get => _selectedSortOption;
            set
            {
                if (_selectedSortOption != value)
                {
                    _selectedSortOption = value;
                    OnPropertyChanged();
                    ApplySort();
                }
            }
        }

        // Доступные варианты сортировки для ComboBox
        public SortOption[] SortOptions => Enum.GetValues<SortOption>();

        // Сервис для работы с БД
        private readonly INoteService _noteService;

        // Сервис для работы с тегами
        private readonly ITagService? _tagService;

        // Сервис для конвертации Markdown в HTML
        private readonly IMarkdownConverter _markdownConverter;

        // Теги текущей выбранной заметки
        public ObservableCollection<Tag> CurrentNoteTags { get; } = new();

        // Все доступные теги для автокомплита
        public ObservableCollection<Tag> AllTags { get; } = new();

        // Команды для тегов
        public ICommand AddTagCommand { get; }
        public ICommand RemoveTagCommand { get; }

        // Команды для фильтрации по тегам
        public ICommand FilterByTagCommand { get; }
        public ICommand ClearTagFilterCommand { get; }

        // Текущий фильтр по тегу
        private Tag? _filterTag;
        public Tag? FilterTag
        {
            get => _filterTag;
            set
            {
                if (_filterTag != value)
                {
                    _filterTag = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsFilteredByTag));
                    ApplyTagFilter();
                }
            }
        }

        // Флаг: активен ли фильтр
        public bool IsFilteredByTag => FilterTag != null;

        // Полный список заметок (без фильтрации) для UI
        private List<NoteListItem> _allNoteItems = new();

        // Режим превью (true = просмотр HTML, false = редактирование Markdown)
        private bool isPreviewMode;
        public bool IsPreviewMode
        {
            get => isPreviewMode;
            set
            {
                if (isPreviewMode != value)
                {
                    isPreviewMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsEditMode));

                    // При переключении в режим превью обновляем HTML
                    if (value)
                    {
                        UpdateHtmlContent();
                    }
                }
            }
        }

        // Обратное свойство для удобства биндинга
        public bool IsEditMode => !IsPreviewMode;

        // HTML-контент для превью
        private string htmlContent = string.Empty;
        public string HtmlContent
        {
            get => htmlContent;
            private set
            {
                if (htmlContent != value)
                {
                    htmlContent = value;
                    OnPropertyChanged();
                }
            }
        }

        // Команда переключения режима
        public ICommand TogglePreviewModeCommand { get; }

        public NotesViewModel()
        {
            var context = DbContextProvider.GetContext();
            _noteService = new NoteService(context);
            _tagService = new TagService(context);
            _markdownConverter = new MarkdownConverter();

            CreateNoteCommand = new RelayCommand(_ => CreateNote());
            AddToFavoritesCommand = new RelayCommand(_ => AddToFavorites(), _ => CanModifyNote());
            DeleteNoteCommand = new RelayCommand(_ => DeleteNote(), _ => CanModifyNote());
            RemoveFromFavoritesCommand = new RelayCommand(_ => RemoveFromFavorites(), _ => SelectedFavoriteItem != null);
            TogglePreviewModeCommand = new RelayCommand(_ => TogglePreviewMode());
            AddTagCommand = new RelayCommand(param => AddTagToCurrentNote(param as string), _ => SelectedNote != null);
            RemoveTagCommand = new RelayCommand(param => RemoveTagFromCurrentNote(param as Tag), _ => SelectedNote != null);
            FilterByTagCommand = new RelayCommand(param => FilterByTag(param as Tag));
            ClearTagFilterCommand = new RelayCommand(_ => ClearTagFilter());

            // Загружаем заметки из БД синхронно для совместимости с тестами
            LoadNotesFromDbAsync().GetAwaiter().GetResult();
            LoadAllTagsAsync().GetAwaiter().GetResult();
        }

        // Конструктор для тестов с переданным сервисом
        public NotesViewModel(INoteService noteService, ITagService? tagService = null)
        {
            _noteService = noteService;
            _tagService = tagService;
            _markdownConverter = new MarkdownConverter();

            CreateNoteCommand = new RelayCommand(_ => CreateNote());
            AddToFavoritesCommand = new RelayCommand(_ => AddToFavorites(), _ => CanModifyNote());
            DeleteNoteCommand = new RelayCommand(_ => DeleteNote(), _ => CanModifyNote());
            RemoveFromFavoritesCommand = new RelayCommand(_ => RemoveFromFavorites(), _ => SelectedFavoriteItem != null);
            TogglePreviewModeCommand = new RelayCommand(_ => TogglePreviewMode());
            AddTagCommand = new RelayCommand(param => AddTagToCurrentNote(param as string), _ => SelectedNote != null);
            RemoveTagCommand = new RelayCommand(param => RemoveTagFromCurrentNote(param as Tag), _ => SelectedNote != null);
            FilterByTagCommand = new RelayCommand(param => FilterByTag(param as Tag));
            ClearTagFilterCommand = new RelayCommand(_ => ClearTagFilter());

            // Загружаем заметки из БД синхронно для совместимости с тестами
            LoadNotesFromDbAsync().GetAwaiter().GetResult();
            if (_tagService != null)
            {
                LoadAllTagsAsync().GetAwaiter().GetResult();
            }
        }

        private bool CanModifyNote() => ActiveListItem != null;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async Task LoadNotesFromDbAsync()
        {
            await LoadNotesFromDbAsyncInternal(null);
        }

        /// <summary>
        /// Перезагрузить заметки из БД с учетом статуса авторизации.
        /// </summary>
        /// <param name="isLoggedIn">true если пользователь авторизован, false если нет, null для автопроверки</param>
        public async Task RefreshNotesAsync(bool? isLoggedIn = null)
        {
            await LoadNotesFromDbAsyncInternal(isLoggedIn);
        }

        private async Task LoadNotesFromDbAsyncInternal(bool? isLoggedIn)
        {
            // Очищаем коллекции перед загрузкой
            AllNotes.Clear();
            Notes.Clear();
            Favorites.Clear();
            _allNoteItems.Clear();
            SelectedListItem = null;
            SelectedNote = null;
            ActiveListItem = null;

            // Загружаем заметки из БД
            var notesFromDb = await _noteService.GetAllNoteAsync();

            // Проверяем наличие дефолтных заметок
            var hasWelcomeNote = notesFromDb.Any(n => n.Title == "Welcome note");
            var hasSecondNote = notesFromDb.Any(n => n.Title == "Second note");

            if (!hasWelcomeNote || !hasSecondNote)
            {
                // Если дефолтных заметок нет, создаем их в БД
                await CreateDefaultNotesInDb(hasWelcomeNote, hasSecondNote);
                // После создания загружаем их из БД
                notesFromDb = await _noteService.GetAllNoteAsync();
            }

            // Если пользователь не авторизован - показываем только дефолтные заметки
            if (isLoggedIn == false)
            {
                notesFromDb = notesFromDb.Where(n => n.Title == "Welcome note" || n.Title == "Second note").ToList();
            }

            // Загружаем заметки в коллекцию
            foreach (var note in notesFromDb)
            {
                AllNotes.Add(note);
                var listItem = CreateListItem(note);
                Notes.Add(listItem);
                _allNoteItems.Add(listItem);

                // Добавляем в избранное, если нужно
                if (note.IsFavorite)
                {
                    Favorites.Add(CreateListItem(note));
                }
            }

            // Применяем сортировку
            ApplySort();
        }

        private async Task CreateDefaultNotesInDb(bool hasWelcomeNote, bool hasSecondNote)
        {
            // Создаем только те дефолтные заметки, которых нет
            if (!hasWelcomeNote)
            {
                var note1 = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Welcome note",
                    Content = "This is a sample note. You can edit it."
                };
                await _noteService.CreateNoteAsync(note1);
            }

            if (!hasSecondNote)
            {
                var note2 = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Second note",
                    Content = "Another sample note to test selection."
                };
                await _noteService.CreateNoteAsync(note2);
            }
        }

        public async Task SaveNoteAsync(Note note)
        {
            if (note == null) return;

            // UpdatedAt обновится автоматически в SaveChangesAsync
            await _noteService.UpdateNoteAsync(note);
        }

        private void AddNote(Note note)
        {
            AllNotes.Add(note);
            var listItem = CreateListItem(note);
            Notes.Add(listItem);
            _allNoteItems.Add(listItem);
        }

        private NoteListItem CreateListItem(Note note)
        {
            return new NoteListItem(note.Id, note.Title, note.CreatedAt, note.UpdatedAt);
        }

        /// <summary>
        /// Применить текущую сортировку к списку заметок.
        /// </summary>
        private void ApplySort()
        {
            // Сохраняем текущее выделение
            var selectedId = SelectedListItem?.Id ?? SelectedFavoriteItem?.Id;

            var sortedNotes = SelectedSortOption switch
            {
                SortOption.TitleAsc => Notes.OrderBy(n => n.Title).ToList(),
                SortOption.TitleDesc => Notes.OrderByDescending(n => n.Title).ToList(),
                SortOption.CreatedDesc => Notes.OrderByDescending(n => n.CreatedAt).ToList(),
                SortOption.CreatedAsc => Notes.OrderBy(n => n.CreatedAt).ToList(),
                SortOption.UpdatedAsc => Notes.OrderBy(n => n.UpdatedAt).ToList(),
                SortOption.UpdatedDesc => Notes.OrderByDescending(n => n.UpdatedAt).ToList(),
                _ => Notes.ToList()
            };

            Notes.Clear();
            foreach (var item in sortedNotes)
            {
                Notes.Add(item);
            }

            // Применяем ту же сортировку к избранному
            var sortedFavorites = SelectedSortOption switch
            {
                SortOption.TitleAsc => Favorites.OrderBy(n => n.Title).ToList(),
                SortOption.TitleDesc => Favorites.OrderByDescending(n => n.Title).ToList(),
                SortOption.CreatedDesc => Favorites.OrderByDescending(n => n.CreatedAt).ToList(),
                SortOption.CreatedAsc => Favorites.OrderBy(n => n.CreatedAt).ToList(),
                SortOption.UpdatedAsc => Favorites.OrderBy(n => n.UpdatedAt).ToList(),
                SortOption.UpdatedDesc => Favorites.OrderByDescending(n => n.UpdatedAt).ToList(),
                _ => Favorites.ToList()
            };

            Favorites.Clear();
            foreach (var item in sortedFavorites)
            {
                Favorites.Add(item);
            }

            // Восстанавливаем выделение
            if (selectedId.HasValue)
            {
                var selectedInNotes = Notes.FirstOrDefault(n => n.Id == selectedId.Value);
                if (selectedInNotes != null)
                {
                    SelectedListItem = selectedInNotes;
                }
                else
                {
                    var selectedInFavorites = Favorites.FirstOrDefault(f => f.Id == selectedId.Value);
                    if (selectedInFavorites != null)
                    {
                        SelectedFavoriteItem = selectedInFavorites;
                    }
                }
            }
        }

        private void UpdateSelectedNote(NoteListItem? listItem)
        {
            if (listItem == null)
            {
                SelectedNote = null;
                ActiveListItem = null;
                CurrentNoteTags.Clear();
                return;
            }

            SelectedNote = AllNotes.Find(n => n.Id == listItem.Id);
            ActiveListItem = listItem;

            // Загружаем теги для выбранной заметки
            Task.Run(async () => await LoadTagsForCurrentNoteAsync());
        }

        // -------------------------------------------------------
        // CRUD операции
        // -------------------------------------------------------

        public void CreateNote()
        {
            var now = DateTime.Now;
            var note = new Note
            {
                Id = Guid.NewGuid(),
                Title = "Unnamed",
                Content = "",
                CreatedAt = now,
                UpdatedAt = now
            };

            AllNotes.Add(note);

            var listItem = CreateListItem(note);
            Notes.Add(listItem);
            _allNoteItems.Add(listItem);

            SelectedListItem = listItem;
            SelectedNote = note;
            ActiveListItem = listItem;

            // Применяем сортировку
            ApplySort();

            // Сохраняем в БД асинхронно (UpdatedAt обновится автоматически в SaveChangesAsync)
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
                note.UpdatedAt = DateTime.Now; // Для UI, в БД обновится автоматически при сохранении
                listItem.UpdatedAt = note.UpdatedAt;

                // Сохраняем изменения в БД (UpdatedAt обновится автоматически в SaveChangesAsync)
                Task.Run(async () => await _noteService.UpdateNoteAsync(note));
            }

            // Обновляем в основном списке, если есть
            var noteItem = Notes.FirstOrDefault(n => n.Id == listItem.Id);
            if (noteItem != null && noteItem != listItem && note != null)
            {
                noteItem.Title = newName;
                noteItem.UpdatedAt = note.UpdatedAt;
            }

            // Обновляем в избранном, если есть
            var favoriteItem = Favorites.FirstOrDefault(f => f.Id == listItem.Id);
            if (favoriteItem != null && favoriteItem != listItem && note != null)
            {
                favoriteItem.Title = newName;
                favoriteItem.UpdatedAt = note.UpdatedAt;
            }

            // Обновляем в _allNoteItems
            var allItem = _allNoteItems.FirstOrDefault(n => n.Id == listItem.Id);
            if (allItem != null && note != null)
            {
                allItem.Title = newName;
                allItem.UpdatedAt = note.UpdatedAt;
            }

            // Применяем сортировку
            ApplySort();
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
                Favorites.Add(CreateListItem(note));
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
            _allNoteItems.RemoveAll(item => item.Id == listItem.Id);

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
            var noteItem = Notes.FirstOrDefault(n => n.Id == listItem.Id);
            if (noteItem != null)
            {
                Notes.Remove(noteItem);
            }

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
            _allNoteItems.RemoveAll(item => item.Id == listItem.Id);

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

        // -------------------------------------------------------
        // Markdown Preview
        // -------------------------------------------------------

        /// <summary>
        /// Переключает режим между редактированием и превью.
        /// </summary>
        public void TogglePreviewMode()
        {
            IsPreviewMode = !IsPreviewMode;
        }

        /// <summary>
        /// Обновляет HTML-контент на основе текущей заметки.
        /// </summary>
        private void UpdateHtmlContent()
        {
            if (SelectedNote == null || string.IsNullOrEmpty(SelectedNote.Content))
            {
                HtmlContent = string.Empty;
                return;
            }

            HtmlContent = _markdownConverter.ConvertToHtml(SelectedNote.Content);
        }

        // -------------------------------------------------------
        // Работа с тегами
        // -------------------------------------------------------

        /// <summary>
        /// Загружает все теги из БД.
        /// </summary>
        private async Task LoadAllTagsAsync()
        {
            if (_tagService == null) return;

            var tags = await _tagService.GetAllTagsAsync();
            AllTags.Clear();
            foreach (var tag in tags)
            {
                AllTags.Add(tag);
            }
        }

        /// <summary>
        /// Загружает теги для текущей заметки.
        /// </summary>
        public async Task LoadTagsForCurrentNoteAsync()
        {
            CurrentNoteTags.Clear();

            if (SelectedNote == null || _tagService == null) return;

            var tags = await _tagService.GetTagsForNoteAsync(SelectedNote.Id);
            foreach (var tag in tags)
            {
                CurrentNoteTags.Add(tag);
            }
        }

        /// <summary>
        /// Добавляет тег к текущей заметке.
        /// </summary>
        private void AddTagToCurrentNote(string? tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName) || SelectedNote == null || _tagService == null)
                return;

            var noteId = SelectedNote.Id;
            var trimmedName = tagName.Trim();

            Task.Run(async () =>
            {
                // Получаем или создаём тег
                var tag = await _tagService.GetOrCreateTagAsync(trimmedName);

                // Добавляем тег к заметке
                await _tagService.AddTagToNoteAsync(noteId, tag.Id);

                // Обновляем UI в главном потоке
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Добавляем в список тегов заметки, если ещё нет
                    if (!CurrentNoteTags.Any(t => t.Id == tag.Id))
                    {
                        CurrentNoteTags.Add(tag);
                    }

                    // Добавляем в общий список тегов, если ещё нет
                    if (!AllTags.Any(t => t.Id == tag.Id))
                    {
                        AllTags.Add(tag);
                    }
                });
            });
        }

        /// <summary>
        /// Удаляет тег из текущей заметки.
        /// </summary>
        private void RemoveTagFromCurrentNote(Tag? tag)
        {
            if (tag == null || SelectedNote == null || _tagService == null)
                return;

            var noteId = SelectedNote.Id;
            var tagId = tag.Id;

            Task.Run(async () =>
            {
                await _tagService.RemoveTagFromNoteAsync(noteId, tagId);

                // Обновляем UI в главном потоке
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var tagToRemove = CurrentNoteTags.FirstOrDefault(t => t.Id == tagId);
                    if (tagToRemove != null)
                    {
                        CurrentNoteTags.Remove(tagToRemove);
                    }
                });
            });
        }

        // -------------------------------------------------------
        // Фильтрация по тегам
        // -------------------------------------------------------

        private void FilterByTag(Tag? tag)
        {
            if (tag == null) return;
            FilterTag = tag;
        }

        private void ClearTagFilter()
        {
            FilterTag = null;
        }

        private void ApplyTagFilter()
        {
            if (_tagService == null) return;

            Task.Run(async () =>
            {
                IEnumerable<NoteListItem> filteredItems;

                if (FilterTag == null)
                {
                    // Показываем все заметки
                    filteredItems = _allNoteItems;
                }
                else
                {
                    // Получаем ID заметок с этим тегом
                    var notesWithTag = await _tagService.GetNotesWithTagAsync(FilterTag.Id);
                    var noteIds = notesWithTag.Select(n => n.Id).ToHashSet();
                    filteredItems = _allNoteItems.Where(item => noteIds.Contains(item.Id));
                }

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Notes.Clear();
                    foreach (var item in filteredItems)
                    {
                        Notes.Add(item);
                    }
                    ApplySort();
                });
            });
        }
    }
}
