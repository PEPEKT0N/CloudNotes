using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    /// Обработчик клавиш в TextBox редактора — здесь выделение текста сохраняется.
    /// </summary>
    private void OnNoteContentKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        switch (e.Key)
        {
            case Key.H:
                ApplySpoilerFormatting();
                e.Handled = true;
                break;
            case Key.B:
                ApplyBoldFormatting();
                e.Handled = true;
                break;
            case Key.I:
                ApplyItalicFormatting();
                e.Handled = true;
                break;
        }
    }

    // -------------------------------------------------------
    // Кнопки форматирования
    // -------------------------------------------------------

    private void OnBoldButtonClick(object? sender, RoutedEventArgs e)
    {
        ApplyBoldFormatting();
    }

    private void OnItalicButtonClick(object? sender, RoutedEventArgs e)
    {
        ApplyItalicFormatting();
    }

    private void OnSpoilerButtonClick(object? sender, RoutedEventArgs e)
    {
        ApplySpoilerFormatting();
    }

    // -------------------------------------------------------
    // Методы форматирования текста
    // -------------------------------------------------------

    /// <summary>
    /// Применяет spoiler-форматирование ||текст|| к выделенному тексту.
    /// </summary>
    private void ApplySpoilerFormatting()
    {
        WrapSelectedText("||", "||");
    }

    /// <summary>
    /// Применяет жирное форматирование **текст** к выделенному тексту.
    /// </summary>
    private void ApplyBoldFormatting()
    {
        WrapSelectedText("**", "**");
    }

    /// <summary>
    /// Применяет курсивное форматирование *текст* к выделенному тексту.
    /// </summary>
    private void ApplyItalicFormatting()
    {
        WrapSelectedText("*", "*");
    }

    /// <summary>
    /// Оборачивает выделенный текст в указанные символы.
    /// Если текст не выделен, вставляет маркеры и ставит курсор между ними.
    /// </summary>
    private void WrapSelectedText(string prefix, string suffix)
    {
        if (_viewModel.SelectedNote == null || _viewModel.IsPreviewMode)
            return;

        var textBox = NoteContentTextBox;
        if (textBox == null)
            return;

        var currentText = textBox.Text ?? string.Empty;
        var selectedText = textBox.SelectedText ?? string.Empty;
        var selectionStart = textBox.SelectionStart;
        var selectionLength = selectedText.Length;

        // Проверка границ
        if (selectionStart < 0)
            selectionStart = 0;
        if (selectionStart > currentText.Length)
            selectionStart = currentText.Length;
        if (selectionStart + selectionLength > currentText.Length)
            selectionLength = currentText.Length - selectionStart;

        if (string.IsNullOrEmpty(selectedText) || selectionLength == 0)
        {
            // Нет выделения — вставляем маркеры и ставим курсор между ними
            var caretIndex = Math.Min(textBox.CaretIndex, currentText.Length);
            if (caretIndex < 0) caretIndex = 0;

            var newText = currentText.Insert(caretIndex, prefix + suffix);
            textBox.Text = newText;
            
            // Ставим курсор между маркерами
            textBox.CaretIndex = caretIndex + prefix.Length;
        }
        else
        {
            // Есть выделение — оборачиваем выделенный текст
            var beforeSelection = currentText.Substring(0, selectionStart);
            var afterSelection = currentText.Substring(selectionStart + selectionLength);
            
            var newText = beforeSelection + prefix + selectedText + suffix + afterSelection;
            textBox.Text = newText;
            
            // Выделяем обёрнутый текст (вместе с маркерами)
            textBox.SelectionStart = selectionStart;
            textBox.SelectionEnd = selectionStart + prefix.Length + selectionLength + suffix.Length;
        }

        // Возвращаем фокус на TextBox
        textBox.Focus();
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
