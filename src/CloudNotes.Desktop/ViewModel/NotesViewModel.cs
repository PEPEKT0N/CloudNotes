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
using CloudNotes.Desktop.Api;
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

        // Единое дерево для отображения папок и заметок
        public ObservableCollection<TreeItem> TreeItems { get; } = new();

        // Папки для отображения в дереве (legacy, для обратной совместимости)
        public ObservableCollection<FolderTreeItem> Folders { get; } = new();

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
                    // Автосохранение: сохраняем текущую заметку перед переключением
                    if (selectedNote != null && _noteServiceFactory != null && !_noteServiceFactory.IsGuestMode)
                    {
                        var noteToSave = selectedNote;
                        Task.Run(async () =>
                        {
                            try
                            {
                                await _noteService.UpdateNoteAsync(noteToSave);
                                System.Diagnostics.Debug.WriteLine($"Auto-saved note (from favorites): {noteToSave.Title}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Auto-save failed: {ex.Message}");
                            }
                        });
                    }

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
                    // Автосохранение: сохраняем текущую заметку перед переключением
                    if (selectedNote != null && _noteServiceFactory != null && !_noteServiceFactory.IsGuestMode)
                    {
                        // Сохраняем асинхронно, не блокируя UI
                        var noteToSave = selectedNote;
                        Task.Run(async () =>
                        {
                            try
                            {
                                await _noteService.UpdateNoteAsync(noteToSave);
                                System.Diagnostics.Debug.WriteLine($"Auto-saved note: {noteToSave.Title}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Auto-save failed: {ex.Message}");
                            }
                        });
                    }

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

        // Команды для папок
        public ICommand CreateFolderCommand { get; }
        public ICommand RenameFolderCommand { get; }
        public ICommand DeleteFolderCommand { get; }
        public ICommand ClearFolderSelectionCommand { get; }
        public ICommand MoveNoteToFolderCommand { get; }
        public ICommand CreateNoteInFolderCommand { get; }
        public ICommand CreateSubfolderCommand { get; }
        public ICommand RenameNoteCommand { get; }

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

        // Фабрика сервисов для переключения между гостевым и авторизованным режимами
        private readonly INoteServiceFactory? _noteServiceFactory;

        // Сервис для работы с БД (используется через фабрику или напрямую для тестов)
        private INoteService _noteService;

        // Сервис для работы с тегами (используется через фабрику или напрямую для тестов)
        private ITagService? _tagService;

        // Сервис для работы с папками
        private FolderService? _folderService;

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

        // Выбранный элемент дерева (папка или заметка)
        private TreeItem? _selectedTreeItem;
        public TreeItem? SelectedTreeItem
        {
            get => _selectedTreeItem;
            set
            {
                if (_selectedTreeItem != value)
                {
                    _selectedTreeItem = value;
                    OnPropertyChanged();

                    // Обновляем SelectedNote если выбрана заметка
                    if (value?.IsNote == true && value.Note != null)
                    {
                        SelectedNote = value.Note;
                        var listItem = _allNoteItems.FirstOrDefault(n => n.Id == value.Note.Id);
                        if (listItem != null)
                        {
                            ActiveListItem = listItem;
                        }
                        Task.Run(async () => await LoadTagsForCurrentNoteAsync());
                    }
                    else if (value?.IsFolder == true)
                    {
                        // Если выбрана папка, сбрасываем выбор заметки
                        SelectedNote = null;
                        ActiveListItem = null;
                    }
                    else
                    {
                        SelectedNote = null;
                        ActiveListItem = null;
                    }

                    // Уведомляем команды об изменении доступности
                    (RenameFolderCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (DeleteFolderCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (CreateNoteInFolderCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (CreateSubfolderCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        // Выбранная папка для фильтрации заметок (legacy, для обратной совместимости)
        private FolderTreeItem? _selectedFolder;
        public FolderTreeItem? SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                if (_selectedFolder != value)
                {
                    _selectedFolder = value;
                    OnPropertyChanged();
                    ApplyFolderFilter();

                    // Уведомляем команды об изменении доступности
                    (ClearFolderSelectionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (RenameFolderCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (DeleteFolderCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

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

            // Пытаемся получить фабрику из DI, если доступна
            _noteServiceFactory = App.ServiceProvider?.GetService(typeof(INoteServiceFactory)) as INoteServiceFactory;

            if (_noteServiceFactory != null)
            {
                // Используем фабрику - по умолчанию гостевой режим
                _noteService = _noteServiceFactory.CurrentNoteService;
                _tagService = _noteServiceFactory.CurrentTagService;
            }
            else
            {
                // Fallback для случаев без DI (тесты и т.д.)
                _noteService = new NoteService(context);
                _tagService = new TagService(context);
            }

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

            // Команды для папок
            CreateFolderCommand = new RelayCommand(_ => CreateFolder());
            RenameFolderCommand = new RelayCommand(_ => RenameFolder(), _ => SelectedTreeItem?.IsFolder == true);
            DeleteFolderCommand = new RelayCommand(_ => DeleteFolder(), _ => SelectedTreeItem?.IsFolder == true);
            ClearFolderSelectionCommand = new RelayCommand(_ => ClearFolderSelection(), _ => SelectedFolder != null);
            MoveNoteToFolderCommand = new RelayCommand(_ => MoveNoteToFolder(), _ => SelectedNote != null);
            CreateNoteInFolderCommand = new RelayCommand(_ => CreateNoteInFolder(), _ => SelectedTreeItem?.IsFolder == true);
            CreateSubfolderCommand = new RelayCommand(_ => CreateSubfolder(), _ => SelectedTreeItem?.IsFolder == true);
            RenameNoteCommand = new RelayCommand(_ => RenameActiveNote(ActiveListItem?.Title ?? ""), _ => SelectedTreeItem?.IsNote == true);

            // Инициализируем FolderService
            var folderContext = DbContextProvider.GetContext();
            var api = App.ServiceProvider?.GetService(typeof(ICloudNotesApi)) as ICloudNotesApi;
            var authService = App.ServiceProvider?.GetService(typeof(IAuthService)) as IAuthService;
            _folderService = new FolderService(folderContext, api, authService);

            // Загружаем заметки синхронно для совместимости с тестами
            // По умолчанию считаем гостевой режим
            LoadNotesFromDbAsync().GetAwaiter().GetResult();
            LoadAllTagsAsync().GetAwaiter().GetResult();
            // Загружаем папки и строим дерево асинхронно после инициализации UI (избегаем deadlock с Dispatcher)
            _ = Task.Run(async () =>
            {
                await Task.Delay(100); // Даем время UI потоку инициализироваться
                await LoadFoldersAsync();
                await BuildTreeAsync();
            });
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

            // Команды для папок (для тестов - заглушки)
            CreateFolderCommand = new RelayCommand(_ => { });
            RenameFolderCommand = new RelayCommand(_ => { }, _ => false);
            DeleteFolderCommand = new RelayCommand(_ => { }, _ => false);

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
            
            // Загружаем папки только если пользователь авторизован
            // LoadFoldersAsync сам проверит гостевой режим и очистит папки
            await LoadFoldersAsync();
        }

        private async Task LoadNotesFromDbAsyncInternal(bool? isLoggedIn)
        {
            // Если статус не передан, определяем безопасно
            if (isLoggedIn == null)
            {
                // По умолчанию считаем неавторизованным при запуске
                isLoggedIn = false;
            }

            // Переключаем режим работы фабрики и обновляем текущие сервисы
            if (_noteServiceFactory != null)
            {
                if (isLoggedIn == true)
                {
                    _noteServiceFactory.SwitchToAuthenticatedMode();
                }
                else
                {
                    _noteServiceFactory.SwitchToGuestMode();
                }
                _noteService = _noteServiceFactory.CurrentNoteService;
                _tagService = _noteServiceFactory.CurrentTagService;
            }

            // Очищаем коллекции перед загрузкой
            AllNotes.Clear();
            Notes.Clear();
            Favorites.Clear();
            _allNoteItems.Clear();
            SelectedListItem = null;
            SelectedNote = null;
            ActiveListItem = null;

            // Загружаем заметки из текущего сервиса
            var notesFromDb = await _noteService.GetAllNoteAsync();

            // Для авторизованного режима создаем дефолтные заметки в БД, если их нет
            if (isLoggedIn == true && _noteServiceFactory != null && !_noteServiceFactory.IsGuestMode)
            {
                var hasWelcomeNote = notesFromDb.Any(n => n.Title == "Welcome note");
                var hasSecondNote = notesFromDb.Any(n => n.Title == "Second note");

                if (!hasWelcomeNote || !hasSecondNote)
                {
                    await CreateDefaultNotesInDb(hasWelcomeNote, hasSecondNote);
                    notesFromDb = await _noteService.GetAllNoteAsync();
                }
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

            // Перестраиваем дерево после загрузки заметок
            if (_folderService != null)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100);
                    await BuildTreeAsync();
                });
            }
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
            // Создание заметки в корне (не в папке)
            var now = DateTime.UtcNow;
            var note = new Note
            {
                Id = Guid.NewGuid(),
                Title = "Unnamed",
                Content = "",
                CreatedAt = now,
                UpdatedAt = now,
                FolderId = null // Всегда в корне
            };

            AllNotes.Add(note);

            var listItem = CreateListItem(note);
            _allNoteItems.Add(listItem);
            Notes.Add(listItem);

            // Сохраняем в БД асинхронно
            Task.Run(async () =>
            {
                await _noteService.CreateNoteAsync(note);
                await BuildTreeAsync();
            });

            SelectedListItem = listItem;
            SelectedNote = note;
            ActiveListItem = listItem;
            ApplySort();

            // Открываем диалог переименования сразу после создания
            Task.Run(async () =>
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var owner = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                        ? desktop.MainWindow
                        : null;

                    var newName = await Views.RenameDialog.ShowDialogAsync(owner, "Unnamed");
                    if (!string.IsNullOrWhiteSpace(newName) && newName != "Unnamed")
                    {
                        RenameActiveNote(newName);
                    }
                });
            });
        }

        public async void CreateNoteInFolder()
        {
            if (SelectedTreeItem?.IsFolder != true || _folderService == null) return;

            var now = DateTime.UtcNow;
            var note = new Note
            {
                Id = Guid.NewGuid(),
                Title = "Unnamed",
                Content = "",
                CreatedAt = now,
                UpdatedAt = now,
                FolderId = SelectedTreeItem.Id
            };

            AllNotes.Add(note);

            var listItem = CreateListItem(note);
            _allNoteItems.Add(listItem);

            // Сохраняем в БД асинхронно
            await _noteService.CreateNoteAsync(note);
            await BuildTreeAsync();

            // Выбираем созданную заметку
            SelectedNote = note;
            ActiveListItem = listItem;

            // Открываем диалог переименования сразу после создания
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var owner = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

                var newName = await Views.RenameDialog.ShowDialogAsync(owner, "Unnamed");
                if (!string.IsNullOrWhiteSpace(newName) && newName != "Unnamed")
                {
                    RenameActiveNote(newName);
                }
            });
        }

        public async void CreateSubfolder()
        {
            if (SelectedTreeItem?.IsFolder != true || _folderService == null) return;

            var owner = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            var folderName = await Views.RenameDialog.ShowDialogAsync(owner, string.Empty);
            if (string.IsNullOrWhiteSpace(folderName))
                return;

            var folder = new Folder
            {
                Id = Guid.NewGuid(),
                Name = folderName.Trim(),
                ParentFolderId = SelectedTreeItem.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _folderService.CreateFolderAsync(folder);
            await LoadFoldersAsync();
            await BuildTreeAsync();
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
                note.UpdatedAt = DateTime.UtcNow; // В БД обновится автоматически при сохранении, но устанавливаем UTC для консистентности
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

        public void AddToFavorites()
        {
            Note? note = null;
            if (SelectedTreeItem?.IsNote == true && SelectedTreeItem.Note != null)
            {
                note = SelectedTreeItem.Note;
            }
            else if (SelectedListItem != null)
            {
                note = AllNotes.FirstOrDefault(n => n.Id == SelectedListItem.Id);
            }

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

        public async void DeleteActiveNote()
        {
            Note? note = null;
            NoteListItem? listItem = null;

            if (SelectedTreeItem?.IsNote == true && SelectedTreeItem.Note != null)
            {
                note = SelectedTreeItem.Note;
                listItem = _allNoteItems.FirstOrDefault(n => n.Id == note.Id);
            }
            else
            {
                listItem = ActiveListItem ?? SelectedListItem;
                if (listItem == null) return;
                note = AllNotes.FirstOrDefault(n => n.Id == listItem.Id);
            }

            if (note == null) return;

            var noteId = note.Id;

            // Удаляем из UI коллекций
            var noteItem = Notes.FirstOrDefault(n => n.Id == noteId);
            if (noteItem != null)
            {
                Notes.Remove(noteItem);
            }

            var favoriteItem = Favorites.FirstOrDefault(f => f.Id == noteId);
            if (favoriteItem != null)
            {
                Favorites.Remove(favoriteItem);
            }

            // Удаляем из данных
            AllNotes.Remove(note);
            _allNoteItems.RemoveAll(item => item.Id == noteId);

            // Удаляем из БД
            await _noteService.DeleteNoteAsync(noteId);
            await BuildTreeAsync();

            // Сбрасываем выбор
            SelectedNote = null;
            SelectedListItem = null;
            ActiveListItem = null;
            SelectedTreeItem = null;
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

                // Применяем фильтр по папке
                if (SelectedFolder != null)
                {
                    var folderId = SelectedFolder.Id;
                    filteredItems = filteredItems.Where(item =>
                    {
                        var note = AllNotes.FirstOrDefault(n => n.Id == item.Id);
                        return note?.FolderId == folderId;
                    });
                }
                // Если папка не выбрана, показываем все заметки (и с папками, и без)

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

        // -------------------------------------------------------
        // Работа с папками
        // -------------------------------------------------------

        private async Task LoadFoldersAsync()
        {
            // Не загружаем папки в гостевом режиме
            if (_folderService == null || _noteServiceFactory?.IsGuestMode == true)
            {
                // Очищаем дерево папок в гостевом режиме
                if (Dispatcher.UIThread.CheckAccess())
                {
                    TreeItems.Clear();
                }
                else
                {
                    await Dispatcher.UIThread.InvokeAsync(() => TreeItems.Clear());
                }
                return;
            }

            // Если мы уже в UI потоке, не нужно использовать InvokeAsync
            if (Dispatcher.UIThread.CheckAccess())
            {
                await LoadFoldersAsyncInternal();
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(async () => await LoadFoldersAsyncInternal());
            }
        }

        private async Task LoadFoldersAsyncInternal()
        {
            Folders.Clear();
            var folders = await _folderService.GetAllFoldersAsync();
            var folderList = folders.ToList();

            // Создаем словарь для быстрого поиска
            var folderDict = folderList.ToDictionary(f => f.Id, f => new FolderTreeItem(f));

            // Строим дерево папок
            var rootFolders = new List<FolderTreeItem>();

            foreach (var folder in folderList)
            {
                var treeItem = folderDict[folder.Id];
                if (folder.ParentFolderId.HasValue && folderDict.TryGetValue(folder.ParentFolderId.Value, out var parent))
                {
                    parent.Children.Add(treeItem);
                }
                else
                {
                    rootFolders.Add(treeItem);
                }
            }

            foreach (var root in rootFolders.OrderBy(f => f.Name))
            {
                Folders.Add(root);
            }
        }

        /// <summary>
        /// Строит единое дерево из папок и заметок.
        /// </summary>
        private async Task BuildTreeAsync()
        {
            if (_folderService == null) return;

            if (Dispatcher.UIThread.CheckAccess())
            {
                await BuildTreeAsyncInternal();
            }
            else
            {
                await Dispatcher.UIThread.InvokeAsync(async () => await BuildTreeAsyncInternal());
            }
        }

        private async Task BuildTreeAsyncInternal()
        {
            TreeItems.Clear();

            // Не строим дерево в гостевом режиме
            if (_noteServiceFactory?.IsGuestMode == true || _folderService == null)
            {
                return;
            }

            // Загружаем все папки
            var folders = await _folderService.GetAllFoldersAsync();
            var folderList = folders.ToList();

            // Создаем словарь для быстрого поиска папок
            var folderDict = folderList.ToDictionary(f => f.Id, f => new TreeItem(f));

            // Строим дерево папок
            var rootFolders = new List<TreeItem>();

            foreach (var folder in folderList)
            {
                var treeItem = folderDict[folder.Id];
                if (folder.ParentFolderId.HasValue && folderDict.TryGetValue(folder.ParentFolderId.Value, out var parent))
                {
                    parent.Children.Add(treeItem);
                }
                else
                {
                    rootFolders.Add(treeItem);
                }
            }

            // Добавляем заметки в соответствующие папки
            foreach (var note in AllNotes)
            {
                var noteItem = new TreeItem(note);
                if (note.FolderId.HasValue && folderDict.TryGetValue(note.FolderId.Value, out var parentFolder))
                {
                    parentFolder.Children.Add(noteItem);
                }
                else
                {
                    // Заметка без папки - добавляем в корень
                    rootFolders.Add(noteItem);
                }
            }

            // Сортируем корневые элементы: сначала папки, потом заметки, внутри каждой группы по имени
            var sortedRoots = rootFolders
                .OrderBy(item => item.IsFolder ? 0 : 1) // Папки первыми
                .ThenBy(item => item.Name)
                .ToList();

            foreach (var root in sortedRoots)
            {
                TreeItems.Add(root);
            }

            // Сортируем дочерние элементы в каждой папке
            void SortChildren(TreeItem item)
            {
                var sortedChildren = item.Children
                    .OrderBy(child => child.IsFolder ? 0 : 1)
                    .ThenBy(child => child.Name)
                    .ToList();
                item.Children.Clear();
                foreach (var child in sortedChildren)
                {
                    item.Children.Add(child);
                    SortChildren(child);
                }
            }

            foreach (var root in TreeItems)
            {
                SortChildren(root);
            }
        }

        private void ApplyFolderFilter()
        {
            // Применяем фильтр по папке вместе с фильтром по тегу
            if (FilterTag != null)
            {
                ApplyTagFilter();
            }
            else
            {
                Task.Run(async () =>
                {
                    IEnumerable<NoteListItem> filteredItems = _allNoteItems;

                    if (SelectedFolder != null)
                    {
                        var folderId = SelectedFolder.Id;
                        filteredItems = filteredItems.Where(item =>
                        {
                            var note = AllNotes.FirstOrDefault(n => n.Id == item.Id);
                            return note?.FolderId == folderId;
                        });
                    }
                    else
                    {
                        // Если папка не выбрана, показываем все заметки (и с папками, и без)
                        // Не применяем фильтр по папке
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

        public async void CreateFolder()
        {
            if (_folderService == null) return;

            // Открываем диалог для ввода имени папки
            var owner = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            var folderName = await Views.RenameDialog.ShowDialogAsync(owner, string.Empty);
            if (string.IsNullOrWhiteSpace(folderName))
                return;

            // Создаем папку в корне
            var folder = new Folder
            {
                Id = Guid.NewGuid(),
                Name = folderName.Trim(),
                ParentFolderId = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _folderService.CreateFolderAsync(folder);
            await LoadFoldersAsync();
            await BuildTreeAsync();
        }

        private async void RenameFolder()
        {
            if (_folderService == null || SelectedTreeItem?.IsFolder != true) return;

            // Открываем диалог для ввода нового имени папки
            var owner = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            var newName = await Views.RenameDialog.ShowDialogAsync(owner, SelectedTreeItem.Name);
            if (string.IsNullOrWhiteSpace(newName))
                return;

            await RenameFolderAsync(newName);
        }

        public async Task RenameFolderAsync(string newName)
        {
            if (_folderService == null || SelectedTreeItem?.IsFolder != true) return;

            var folder = SelectedTreeItem.Folder!;
            folder.Name = newName.Trim();
            folder.UpdatedAt = DateTime.UtcNow;

            await _folderService.UpdateFolderAsync(folder);
            await LoadFoldersAsync();
            await BuildTreeAsync();
        }

        public async void DeleteFolder()
        {
            if (_folderService == null || SelectedTreeItem?.IsFolder != true) return;

            // TODO: Показать подтверждение удаления
            await _folderService.DeleteFolderAsync(SelectedTreeItem.Id);
            SelectedTreeItem = null;
            await LoadFoldersAsync();
            await BuildTreeAsync();
        }

        private void ClearFolderSelection()
        {
            // Явно сбрасываем выбор папки
            _selectedFolder = null;
            OnPropertyChanged(nameof(SelectedFolder));
            ApplyFolderFilter();

            // Уведомляем команды об изменении доступности
            (ClearFolderSelectionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RenameFolderCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteFolderCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        public async void MoveNoteToFolder()
        {
            Note? note = null;
            if (SelectedTreeItem?.IsNote == true && SelectedTreeItem.Note != null)
            {
                note = SelectedTreeItem.Note;
            }
            else if (SelectedNote != null)
            {
                note = SelectedNote;
            }

            if (note == null) return;

            // Получаем список всех папок для выбора (включая вложенные)
            // Создаем специальную папку для "(No folder)"
            var noFolder = new CloudNotes.Desktop.Model.Folder { Id = Guid.Empty, Name = "(No folder)" };
            var folders = new List<FolderTreeItem> { new FolderTreeItem(noFolder) };

            // Рекурсивно собираем все папки
            void CollectFolders(ObservableCollection<FolderTreeItem> folderCollection)
            {
                foreach (var folder in folderCollection)
                {
                    folders.Add(folder);
                    if (folder.Children.Count > 0)
                    {
                        CollectFolders(folder.Children);
                    }
                }
            }
            CollectFolders(Folders);

            // Открываем диалог для выбора папки
            var owner = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            var selectedFolder = await Views.FolderSelectionDialog.ShowDialogAsync(owner, folders, note.FolderId);

            if (selectedFolder == null) return; // Пользователь отменил

            // Обновляем FolderId заметки
            var newFolderId = selectedFolder.Id == Guid.Empty ? (Guid?)null : selectedFolder.Id;

            // Проверяем, изменилась ли папка
            if (note.FolderId == newFolderId) return; // Папка не изменилась

            note.FolderId = newFolderId;
            note.UpdatedAt = DateTime.UtcNow;

            // Сохраняем изменения
            await SaveNoteAsync(note);
            await BuildTreeAsync();

            // Обновляем SelectedNote
            SelectedNote = note;
        }
    }
}
