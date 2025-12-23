using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CloudNotes.Desktop.Model;
using CloudNotes.Desktop.Services;
using CloudNotes.Desktop.ViewModel;
using Microsoft.Extensions.DependencyInjection;

namespace CloudNotes.Desktop.Views;

public partial class MainWindow : Window
{
    private NotesViewModel _viewModel;
    private IConflictService? _conflictService;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new NotesViewModel();
        DataContext = _viewModel;

        NoteListViewControl.DataContext = _viewModel;

        // Получаем ConflictService из DI
        _conflictService = App.ServiceProvider?.GetService<IConflictService>();
        if (_conflictService != null)
        {
            _conflictService.ConflictDetected += OnConflictDetected;
        }

        // Глобальные горячие клавиши
        KeyDown += OnKeyDown;
    }

    private async void OnConflictDetected(NoteConflict conflict)
    {
        // Показываем диалог разрешения конфликта в UI потоке
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = this;
            var result = await ConflictResolutionDialog.ShowDialogAsync(owner, conflict);

            if (result.HasValue && _conflictService != null)
            {
                // true = использовать локальную версию, false = использовать серверную версию
                await _conflictService.ResolveConflictAsync(conflict.LocalNoteId, !result.Value);
            }
        });
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
