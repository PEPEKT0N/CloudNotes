using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CloudNotes.Desktop.ViewModel;
using CloudNotes.Desktop.Model;

namespace CloudNotes.Desktop.Views;

public partial class NoteListView : UserControl
{
    public NoteListView()
    {
        InitializeComponent();

        // Подписываемся на горячие клавиши
        KeyDown += OnKeyDown;
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not NotesViewModel vm)
            return;

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        switch (e.Key)
        {
            // Ctrl+R — переименовать
            case Key.R when ctrl:
                e.Handled = true;  // Важно: ставим ДО await, чтобы "R" не попала в диалог
                await RenameSelectedNoteAsync(vm);
                break;

            // Ctrl+D — удалить заметку
            case Key.D when ctrl:
                vm.DeleteActiveNote();
                e.Handled = true;
                break;

            // Ctrl+S — сохранить (пока заглушка)
            case Key.S when ctrl:
                SaveNotes();
                e.Handled = true;
                break;
        }
    }

    private async Task RenameSelectedNoteAsync(NotesViewModel vm)
    {
        var listItem = vm.ActiveListItem ?? vm.SelectedListItem;
        if (listItem == null) return;

        var owner = this.VisualRoot as Window;
        var result = await RenameDialog.ShowDialogAsync(owner, listItem.Title);

        if (!string.IsNullOrWhiteSpace(result))
        {
            vm.RenameActiveNote(result);
        }
    }

    private void SaveNotes()
    {
        // TODO: Реализовать сохранение в БД
        System.Diagnostics.Debug.WriteLine("Ctrl+S pressed — Save notes (not implemented yet)");
    }

    private void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not NotesViewModel vm)
            return;

        if (sender is ListBox listBox)
        {
            vm.SelectedListItem = listBox.SelectedItem as NoteListItem;
        }
    }

    private void OnFavoritesSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not NotesViewModel vm)
            return;

        if (sender is ListBox listBox)
        {
            vm.SelectedFavoriteItem = listBox.SelectedItem as NoteListItem;
        }
    }

    private async void OnRenameMenuClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not NotesViewModel vm)
            return;

        var listItem = vm.ActiveListItem ?? vm.SelectedListItem;
        if (listItem == null)
            return;

        var owner = this.VisualRoot as Window;
        var result = await RenameDialog.ShowDialogAsync(owner, listItem.Title);

        if (!string.IsNullOrWhiteSpace(result))
        {
            vm.RenameActiveNote(result);
        }
    }
}
