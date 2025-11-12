using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel; // для INotifyPropertyChanged
using System.Runtime.CompilerServices; // для [CallerMemberName]
using Avalonia.Controls;
using CloudNotes.Desktop.Model;

namespace CloudNotes;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private NoteListItem? selectedListItem;
    private Note? selectedNote;

    public ObservableCollection<NoteListItem> Notes { get; set; }
    private readonly List<Note> allNotes = new();

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
                OnPropertyChanged(); // уведомляем об изменении SelectedNote
            }
        }
    }

    public MainWindow()
    {
        InitializeComponent();

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
            Notes.Add(new NoteListItem
            {
                Id = note.Id,
                Title = note.Title,
                UpdatedAt = note.UpdatedAt
            });
        }

        DataContext = this;
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
