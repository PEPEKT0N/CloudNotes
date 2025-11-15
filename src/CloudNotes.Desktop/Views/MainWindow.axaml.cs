using Avalonia.Controls;
using CloudNotes.Desktop.ViewModel;
using CloudNotes.Desktop.Model;

namespace CloudNotes.Desktop.Views;

public partial class MainWindow : Window
{
    public NotesViewModel ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new NotesViewModel();
        DataContext = this;
    }

    private void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is NoteListItem listItem)
        {
            ViewModel.OnNoteSelected(listItem);
        }
    }
}
