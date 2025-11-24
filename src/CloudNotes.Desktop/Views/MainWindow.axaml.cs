using Avalonia.Controls;
using CloudNotes.Desktop.ViewModel;
using CloudNotes.Desktop.Model;
using System.Linq;
using CloudNotes.Desktop.Services;

namespace CloudNotes.Desktop.Views;

public partial class MainWindow : Window
{
    public NotesViewModel ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();
        var noteService = new NoteService();

        var viewModel = new NotesViewModel(noteService);

        DataContext = viewModel;
    }

    private void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is NoteListItem listItem)
        {
            ViewModel.OnNoteSelected(listItem);
        }
    }

    private void OnFavoritesSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not NotesViewModel vm)
        {
            return;
        }

        if (sender is ListBox listBox)
        {
            var selected = listBox.SelectedItem as NoteListItem;
            vm.SelectedListItem = selected;

            if (selected != null)
            {
                var note = vm.AllNotes.FirstOrDefault(n => n.Id == selected.Id);
                vm.SelectedNote = note;
            }
        }
    }
}
