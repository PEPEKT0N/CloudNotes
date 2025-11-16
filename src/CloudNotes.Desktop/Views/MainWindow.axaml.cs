using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel; // для INotifyPropertyChanged
using System.Linq;
using System.Runtime.CompilerServices; // для [CallerMemberName]
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using CloudNotes.Desktop.ViewModel;
using CloudNotes.Desktop.Model;
using CloudNotes.Desktop.Services;

namespace CloudNotes.Desktop.Views;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private NoteListItem? selectedListItem;
    private Note? selectedNote;
    private readonly NoteService _noteService;

    public ObservableCollection<NoteListItem> Notes { get; set; } = new ObservableCollection<NoteListItem>();
    private readonly List<Note> allNotes = new(); // хранит полные поля Note

    // Команды для кнопок
    public ICommand CreateNoteCommand { get; }
    public ICommand UpdateNoteCommand { get; }
    public ICommand DeleteNoteCommand { get; }

    public new event PropertyChangedEventHandler? PropertyChanged;


    // Свойство SelectedListItem (для списка в XAML)
    public NoteListItem? SelectedListItem
    {
        get => selectedListItem;
        set
        {
            if (selectedListItem != value)
            {
                selectedListItem = value;
                OnNoteSelected(selectedListItem);
                OnPropertyChanged();
            }
        }
    }

    public Note? SelectedNote
    {
        get => selectedNote;
        set
        {
            if (selectedNote != value)
            {
                selectedNote = value;
                Console.WriteLine($"SelectedNote изменилось. Новое значение: {value?.Content}");
                OnPropertyChanged(); // уведомляем об изменении SelectedNote
            }
        }
    }


    public MainWindow()
    {
        InitializeComponent();

        // Инициализация сервиса
        _noteService = new NoteService();

        // Инициализация команд (передаем методы, которые выполняются)
        CreateNoteCommand = new RelayCommand(async (param) => await CreateNewNoteAsync());
        UpdateNoteCommand = new RelayCommand(async (param) => await UpdateSelectedNoteAsync(), (param) => SelectedNote != null);
        DeleteNoteCommand = new RelayCommand(async (param) => await DeleteSelectedNoteAsync(), (param) => SelectedNote != null);

        // Загрузка заметок из БД при запуске
        LoadNotesFromDatabase();
        DataContext = this;
    }


    // Метод для загрузки заметок из БД
    private async void LoadNotesFromDatabase()
    {
        try
        {
            var notes = await _noteService.GetAllNoteAsync();
            allNotes.Clear();
            allNotes.AddRange(notes);

            // Обноеление ObservableCollection для UI
            Notes.Clear();
            foreach (var note in allNotes)
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
            Console.WriteLine($"Ошибка при загрузке заметок: {ex.Message}");
        }
    }

    // Метод, вызываемый при выборе заметки в списке
    public void OnNoteSelected(NoteListItem? listItem)
    {
        if (listItem == null)
        {
            Console.WriteLine("OnNoteSelected: listItem is null, SelectedNote будет установлен в null.");
            SelectedNote = null;

            // обновление CanExecute для команд Update и Delete
            ((RelayCommand)UpdateNoteCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DeleteNoteCommand).RaiseCanExecuteChanged();
            return;
        }

        Console.WriteLine($"OnNoteSelected: Выбран listItem с Id: {listItem.Id}, Title: {listItem.Title}");

        // поиск полного объекта Note по Id
        SelectedNote = allNotes.FirstOrDefault(n => n.Id == listItem.Id);
        Console.WriteLine($"Выбрана заметка с Id: {SelectedNote?.Id}, Title: {SelectedNote?.Title}");

        ((RelayCommand)UpdateNoteCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DeleteNoteCommand).RaiseCanExecuteChanged();
    }

    // Метод для создания новой заметки
    private async Task CreateNewNoteAsync()
    {
        try
        {
            var newNote = new Note
            {
                Id = Guid.NewGuid(),
                Title = "New Note",
                Content = "Content of the new note.",
                UpdatedAt = DateTime.UtcNow
            };

            var createdNote = await _noteService.CreateNoteAsync(newNote);

            // добавление в локальные коллекции
            allNotes.Add(createdNote);
            Notes.Add(new NoteListItem
            {
                Id = createdNote.Id,
                Title = createdNote.Title,
                UpdatedAt = createdNote.UpdatedAt
            });

            // Выбор новой заметки
            SelectedListItem = Notes.Last();
            //ЛОГГИРОВАНИЕ: После создания
            Console.WriteLine($"CreateNewNoteAsync: Создана новая заметка с Id: {createdNote.Id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при создании заметки: {ex.Message}");
        }
    }

    // Метод для обновления выбранной заметки
    private async Task UpdateSelectedNoteAsync()
    {
        if (SelectedNote == null)
        {
            Console.WriteLine("SelectedNote is null");
            return;
        }

        try
        {

            //ЛОГГИРОВАНИЕ: Перед сохранением
            Console.WriteLine($"UpdateSelectedNoteAsync: Пытаемся обновить заметку с Id: {SelectedNote.Id}, Content: '{SelectedNote.Content}'");

            // Обновление времени перед сохранением
            SelectedNote.UpdatedAt = DateTime.UtcNow;

            await _noteService.UpdateNoteAsync(SelectedNote);

            // обновдение элемента в ObservableCollection (UI)
            var listItem = Notes.FirstOrDefault(n => n.Id == SelectedNote.Id);
            if (listItem != null)
            {
                listItem.Title = SelectedNote.Title;
                listItem.UpdatedAt = SelectedNote.UpdatedAt;
            }

            // Уведомляем UI, что SelectedNote изменилось (например, если Title отображается где-то еще)
            OnPropertyChanged(nameof(SelectedNote));

            //  ЛОГГИРОВАНИЕ: После успешного обновления
            Console.WriteLine($"UpdateSelectedNoteAsync: Заметка с Id: {SelectedNote.Id} успешно обновлена в БД.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при обновлении заметки: {ex.Message}");
        }
    }

    // Метод для удаления выбранной заметки
    private async Task DeleteSelectedNoteAsync()
    {
        if (SelectedNote == null)
        {
            Console.WriteLine("DeleteSelectedNoteAsync: SelectedNote is null, удаление невозможно.");
            return;
        }

        try
        {
            // --- ЛОГГИРОВАНИЕ: Перед удалением ---
            Console.WriteLine($"DeleteSelectedNoteAsync: Пытаемся удалить заметку с Id: {SelectedNote.Id}");

            var success = await _noteService.DeleteNoteAsync(SelectedNote.Id);
            if (success)
            {
                // удаление из локальных коллекций
                allNotes.Remove(SelectedNote);
                var listItem = Notes.FirstOrDefault(n => n.Id == SelectedNote.Id);
                if (listItem != null)
                {
                    Notes.Remove(listItem);
                }

                SelectedNote = null;
                SelectedListItem = null;
                // Обновляем CanExecute для команд Update и Delete
                ((RelayCommand)UpdateNoteCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeleteNoteCommand).RaiseCanExecuteChanged();

                // --- ЛОГГИРОВАНИЕ: После успешного удаления ---
                Console.WriteLine($"DeleteSelectedNoteAsync: Заметка с Id: {SelectedNote.Id} успешно удалена из БД и коллекций.");
            }
            else
            {
                // --- ЛОГГИРОВАНИЕ: Если удаление не удалось ---
                Console.WriteLine($"DeleteSelectedNoteAsync: Заметка с Id: {SelectedNote.Id} не найдена в БД для удаления.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при удалении заметки: {ex.Message}");
        }
    }

    // Вспомогательный метод для уведомления UI об изменении свойства
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Обработчик SelectionChanged
    private void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var listBox = sender as ListBox;
        var selectedItem = listBox?.SelectedItem as NoteListItem;
        OnNoteSelected(selectedItem);
    }

}

// реализация ICommand для асинхронных команд
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
