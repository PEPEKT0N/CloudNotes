using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using CloudNotes.Desktop.Model;

namespace CloudNotes;

public partial class MainWindow : Window
{
    public ObservableCollection<NoteListItem> Notes { get; set; }
    public MainWindow()
    {
        InitializeComponent();

        Notes = new ObservableCollection<NoteListItem>
        {
            new NoteListItem {Title = "First Note", UpdatedAt = DateTime.Now },
            new NoteListItem { Title = "Second Note", UpdatedAt = DateTime.Now.AddMinutes(-10) }
        };

        NotesListBox.ItemsSource = Notes;
    }
}
