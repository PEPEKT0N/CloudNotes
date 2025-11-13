using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Controls;
using CloudNotes.Desktop.Model;

namespace CloudNotes;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private NoteListItem? selectedListItem;
    private Note? selectedNote;

    public ObservableCollection<NoteListItem> Notes { get; set; }
    private readonly List<Note> allNotes = new();
    public ICommand CreateNoteCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;

    public NoteListItem? SelectedListItem
    {
        get => selectedListItem;
        set
        {
            if (selectedListItem != value)
            {
                selectedListItem = value;
                OnNoteSelected(selectedListItem);
                OnPropertyChanged(); // уведомляем об изменении SelectedListItem
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
                OnPropertyChanged();
            }
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        // TODO: Убрать после завершения этапа 1
        allNotes.Add(new Note
        {
            Id = Guid.NewGuid(),
            Title = "First note",
            Content = "This is the content of the first note.",
            UpdatedAt = DateTime.Now
        });
        allNotes.Add(new Note
        {
            Id = Guid.NewGuid(),
            Title = "Second note",
            Content = "This is the content of the second note.",
            UpdatedAt = DateTime.Now.AddMinutes(-10)
        });

        Notes = new ObservableCollection<NoteListItem>();
        foreach (var note in allNotes)
        {
            Notes.Add(GenerateListItem(note));
        }

        CreateNoteCommand = new RelayCommand(_ => CreateNote());

        DataContext = this;
    }

    private void CreateNote()
    {
        var newNote = new Note
        {
            Id = Guid.NewGuid(),
            Title = "Unnamed",
            Content = "",
            UpdatedAt = DateTime.Now
        };

        allNotes.Add(newNote);

        var newListItem = GenerateListItem(newNote);

        // var newListItem = new NoteListItem
        // {
        //     Id = newNote.Id,
        //     Title = newNote.Title,
        //     UpdatedAt = newNote.UpdatedAt
        // };

        Notes.Add(newListItem);

        SelectedListItem = newListItem;
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

    public void OnNoteSelected(NoteListItem? listItem)
    {
        if (listItem == null)
        {
            SelectedNote = null;
            return;
        }

        SelectedNote = allNotes.Find(n => n.Id == listItem.Id);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // Обработчик SelectionChanged можно удалить, если используешь только биндинг
    private void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var listBox = sender as ListBox;
        var selectedItem = listBox?.SelectedItem as NoteListItem;
        OnNoteSelected(selectedItem);
    }
}
