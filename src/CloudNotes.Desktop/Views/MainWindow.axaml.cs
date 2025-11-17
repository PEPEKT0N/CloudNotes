using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using CloudNotes.Desktop.Model;
using CloudNotes.Desktop.Services;

namespace CloudNotes.Desktop.Views;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly NoteService _noteService;
    private NoteListItem? _selectedListItem;
    private Note? _selectedNote;

    // --- Свойства для привязки к UI ---

    // Коллекция для отображения списка заметок
    public ObservableCollection<NoteListItem> Notes { get; }
        = new ObservableCollection<NoteListItem>();

    // Свойство для отслеживания выбранного элемента в списке
    public NoteListItem? SelectedListItem
    {
        get => _selectedListItem;
        set
        {
            if (_selectedListItem != value)
            {
                _selectedListItem = value;
                // При выборе элемента в списке, устанавливаем соответствующую заметку
                SelectedNote = _selectedListItem != null
                    ? _noteService.GetAllNoteAsync().Result.FirstOrDefault(n => n.Id == _selectedListItem.Id) // Не используем .Result на UI потоке!
                    : null;
                OnPropertyChanged();
            }
        }
    }

    // Свойство для отслеживания выбранной заметки (для редактирования)
    public Note? SelectedNote
    {
        get => _selectedNote;
        set
        {
            if (_selectedNote != value)
            {
                _selectedNote = value;
                OnPropertyChanged();
                ((RelayCommand)UpdateNoteCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeleteNoteCommand).RaiseCanExecuteChanged();
            }
        }
    }

    // Команды для кнопок
    public ICommand CreateNoteCommand { get; }
    public ICommand UpdateNoteCommand { get; }
    public ICommand DeleteNoteCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;


    public MainWindow()
    {
        _noteService = new NoteService();

        InitializeComponent();

        CreateNoteCommand = new RelayCommand(async (param) => await CreateNoteAsync(), (param) => true);
        UpdateNoteCommand = new RelayCommand(async (param) => await UpdateNoteAsync(), (param) => SelectedNote != null);
        DeleteNoteCommand = new RelayCommand(async (param) => await DeleteNoteAsync(), (param) => SelectedNote != null);

        // Загрузка заметок  заметок при старте
        LoadNotesAsync();

        // Устанавливаем себя как контекст данных для привязки
        DataContext = this;
    }

    // --- Методы для CRUD-операций ---
    // Загрузка списка заметок
    private async void LoadNotesAsync()
    {
        try
        {
            Notes.Clear();

            // Получаем все заметки из сервиса (асинхронно)
            var notes = await _noteService.GetAllNoteAsync();

            // Добавляем каждую заметку в ObservableCollection как NoteListItem
            foreach (var note in notes)
            {
                Notes.Add(new NoteListItem
                {
                    Id = note.Id,
                    Title = note.Title,
                    UpdatedAt = note.UpdatedAt
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки заметок: {ex.Message}");
        }
    }

    // Создание новой заметки
    private async Task CreateNoteAsync()
    {
        try
        {
            var newNote = new Note
            {
                Id = Guid.NewGuid(),
                Title = "Новая заметка",
                Content = "Содержимое новой заметки...",
                UpdatedAt = DateTime.UtcNow
            };

            // Сохраняем через сервис (асинхронно)
            var createdNote = await _noteService.CreateNoteAsync(newNote);

            Notes.Add(new NoteListItem
            {
                Id = createdNote.Id,
                Title = createdNote.Title,
                UpdatedAt = createdNote.UpdatedAt
            });

            SelectedListItem = Notes.Last();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка создания заметки: {ex.Message}");
        }
    }

    // Обновление выбранной заметки
    private async Task UpdateNoteAsync()
    {
        if (SelectedNote == null) return;

        try
        {
            // SelectedNote.Content и другие поля уже обновлены через Binding
            // Обновляем время
            SelectedNote.UpdatedAt = DateTime.UtcNow;

            // Сохраняем через сервис (асинхронно)
            await _noteService.UpdateNoteAsync(SelectedNote);

            // Обновляем соответствующий элемент в UI коллекции (если Title изменился)
            var listItem = Notes.FirstOrDefault(n => n.Id == SelectedNote.Id);
            if (listItem != null)
            {
                listItem.Title = SelectedNote.Title;
                listItem.UpdatedAt = SelectedNote.UpdatedAt;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обновления заметки: {ex.Message}");
        }
    }

    // Удаление выбранной заметки
    private async Task DeleteNoteAsync()
    {
        if (SelectedNote == null) return;

        try
        {
            // Удаляем через сервис (асинхронно)
            var success = await _noteService.DeleteNoteAsync(SelectedNote.Id);
            if (success)
            {
                // Удаляем из UI коллекции
                var listItem = Notes.FirstOrDefault(n => n.Id == SelectedNote.Id);
                if (listItem != null)
                {
                    Notes.Remove(listItem);
                }

                // Сбрасываем выбор
                SelectedNote = null;
                SelectedListItem = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка удаления заметки: {ex.Message}");
        }
    }

    //  Вспомогательный метод для INotifyPropertyChanged ---
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// --- Реализация ICommand (RelayCommand) ---
// Простая реализация для асинхронных команд
public class RelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool> _canExecute;

    public event EventHandler? CanExecuteChanged;

    public RelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute ?? (_ => true);
    }

    public bool CanExecute(object? parameter) => _canExecute(parameter);

    public async void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            await _execute(parameter);
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}