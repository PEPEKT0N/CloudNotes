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

    private async void OnKeyDown(object? sender, KeyEventArgs e)
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

        // Ctrl+L — открыть окно авторизации (временно для тестирования)
        if (e.Key == Key.L && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var result = await AuthWindow.ShowDialogAsync(this);
            if (result != null)
            {
                System.Diagnostics.Debug.WriteLine($"Auth result: IsLogin={result.IsLogin}, Email={result.Email}");
            }
            e.Handled = true;
        }
    }

    /// <summary>
    /// Обработчик нажатия Enter в поле автокомплита тега.
    /// </summary>
    private void OnTagAutoCompleteKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is AutoCompleteBox autoCompleteBox)
        {
            string? tagName = null;

            // Если выбран существующий тег из списка
            if (autoCompleteBox.SelectedItem is CloudNotes.Desktop.Model.Tag selectedTag)
            {
                tagName = selectedTag.Name;
            }
            // Иначе берём введённый текст
            else if (!string.IsNullOrWhiteSpace(autoCompleteBox.Text))
            {
                tagName = autoCompleteBox.Text.Trim();
            }

            if (!string.IsNullOrEmpty(tagName))
            {
                _viewModel.AddTagCommand.Execute(tagName);
                autoCompleteBox.Text = string.Empty;
                autoCompleteBox.SelectedItem = null;
            }
            e.Handled = true;
        }
    }
}
