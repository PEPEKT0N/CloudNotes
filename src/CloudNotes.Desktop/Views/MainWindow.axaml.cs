using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using CloudNotes.Desktop.ViewModel;

namespace CloudNotes.Desktop.Views;

public partial class MainWindow : Window
{
    private NotesViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new NotesViewModel();
        DataContext = _viewModel;

        NoteListViewControl.DataContext = _viewModel;

        // Глобальные горячие клавиши
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl+N — создать заметку (работает всегда)
        if (e.Key == Key.N && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _viewModel.CreateNote();
            NoteListViewControl.Focus();  // Передаём фокус на список
            e.Handled = true;
        }

        // Ctrl+E — переключение между режимом редактирования и превью
        if (e.Key == Key.E && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _viewModel.TogglePreviewMode();
            e.Handled = true;
        }
    }
}
