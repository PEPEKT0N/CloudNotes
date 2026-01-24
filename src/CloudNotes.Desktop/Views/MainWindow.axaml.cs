using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
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
    private IAuthService? _authService;
    private List<int> _searchMatches = new List<int>();
    private int _currentMatchIndex = -1;
    private string _lastSearchText = string.Empty;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new NotesViewModel();
        DataContext = _viewModel;

        NoteListViewControl.DataContext = _viewModel;

        // Получаем сервисы из DI
        _conflictService = App.ServiceProvider?.GetService<IConflictService>();
        _authService = App.ServiceProvider?.GetService<IAuthService>();

        if (_conflictService != null)
        {
            _conflictService.ConflictDetected += OnConflictDetected;
        }

        // Глобальные горячие клавиши
        KeyDown += OnKeyDown;

        // Подписываемся на изменения текста для обновления подсветки
        if (NoteContentTextBox != null)
        {
            NoteContentTextBox.TextChanged += OnNoteContentTextChanged;
        }
    }

    private void OnNoteContentTextChanged(object? sender, TextChangedEventArgs e)
    {
        // Обновляем подсветку при изменении текста
        if (!string.IsNullOrEmpty(_lastSearchText) && _searchMatches.Count > 0)
        {
            Dispatcher.UIThread.Post(() =>
            {
                PerformSearch(_lastSearchText);
            }, DispatcherPriority.Background);
        }
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

        // Ctrl+F — открыть панель поиска
        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            ShowSearchPanel();
            e.Handled = true;
        }

        // Esc — закрыть панель поиска
        if (e.Key == Key.Escape && SearchPanel != null && SearchPanel.IsVisible)
        {
            HideSearchPanel();
            e.Handled = true;
        }

        // F3 — следующее совпадение
        if (e.Key == Key.F3 && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            FindNextMatch();
            e.Handled = true;
        }

        // Shift+F3 — предыдущее совпадение
        if (e.Key == Key.F3 && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            FindPreviousMatch();
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
            case Key.T:
                InsertCurrentDateTime();
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

    private void OnFlashcardButtonClick(object? sender, RoutedEventArgs e)
    {
        InsertFlashcardTemplate();
    }

    private async void OnStudyButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedNote == null)
            return;

        var noteId = _viewModel.SelectedNote.Id;
        var content = _viewModel.SelectedNote.Content;
        var flashcards = FlashcardParser.Parse(content);

        if (flashcards.Count == 0)
        {
            return;
        }

        // Получаем email пользователя для привязки статистики
        string? userEmail = null;
        if (_authService != null)
        {
            userEmail = await _authService.GetCurrentUserEmailAsync();
        }

        await StudyDialog.ShowDialogAsync(this, flashcards, noteId, userEmail);
    }

    private async void OnStudyAllButtonClick(object? sender, RoutedEventArgs e)
    {
        // Получаем email пользователя
        string? userEmail = null;
        if (_authService != null)
        {
            userEmail = await _authService.GetCurrentUserEmailAsync();
        }

        // Открываем диалог выбора тегов
        var (confirmed, tagIds) = await TagSelectionDialog.ShowDialogAsync(this, userEmail);

        if (!confirmed || tagIds.Count == 0)
        {
            return;
        }

        // Получаем карточки по выбранным тегам
        var context = CloudNotes.Services.DbContextProvider.GetContext();
        var tagService = new TagService(context);
        var cards = await tagService.GetFlashcardsByTagsAsync(tagIds);

        if (cards.Count == 0)
        {
            // Нет карточек для изучения
            return;
        }

        // Открываем диалог обучения
        await StudyDialog.ShowDialogByTagsAsync(this, cards, userEmail);
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
    /// Вставляет текущую дату и время в формате Windows Notepad (dd.MM.yyyy HH:mm:ss).
    /// </summary>
    private void InsertCurrentDateTime()
    {
        if (_viewModel.SelectedNote == null || _viewModel.IsPreviewMode)
            return;

        var textBox = NoteContentTextBox;
        if (textBox == null)
            return;

        // Формат как в Windows Notepad: dd.MM.yyyy HH:mm:ss
        var dateTimeString = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");

        var currentText = textBox.Text ?? string.Empty;
        var caretIndex = Math.Min(textBox.CaretIndex, currentText.Length);
        if (caretIndex < 0) caretIndex = 0;

        // Вставляем дату/время в позицию курсора
        var newText = currentText.Insert(caretIndex, dateTimeString);
        textBox.Text = newText;

        // Перемещаем курсор после вставленного текста
        textBox.CaretIndex = caretIndex + dateTimeString.Length;

        // Возвращаем фокус на TextBox
        textBox.Focus();
    }

    /// <summary>
    /// Вставляет шаблон карточки ??question::answer?? в текст.
    /// </summary>
    private void InsertFlashcardTemplate()
    {
        if (_viewModel.SelectedNote == null || _viewModel.IsPreviewMode)
            return;

        var textBox = NoteContentTextBox;
        if (textBox == null)
            return;

        var currentText = textBox.Text ?? string.Empty;
        var selectedText = textBox.SelectedText ?? string.Empty;
        var caretIndex = textBox.CaretIndex;

        if (caretIndex < 0) caretIndex = 0;
        if (caretIndex > currentText.Length) caretIndex = currentText.Length;

        string template;
        int cursorOffset;

        if (!string.IsNullOrEmpty(selectedText))
        {
            // Если есть выделение — используем его как вопрос
            template = $"??{selectedText}::answer??";
            cursorOffset = selectedText.Length + 4; // позиция после "::" для ввода ответа
        }
        else
        {
            // Вставляем пустой шаблон
            template = "??question::answer??";
            cursorOffset = 2; // позиция после "??" для ввода вопроса
        }

        var newText = currentText.Insert(caretIndex, template);
        textBox.Text = newText;
        textBox.CaretIndex = caretIndex + cursorOffset;
        textBox.Focus();
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

    // -------------------------------------------------------
    // Поиск текста
    // -------------------------------------------------------

    private void ShowSearchPanel()
    {
        if (SearchPanel == null || SearchTextBox == null)
            return;

        SearchPanel.IsVisible = true;
        SearchTextBox.Focus();

        // Если есть выделенный текст, используем его как поисковый запрос
        var textBox = NoteContentTextBox;
        if (textBox != null && !string.IsNullOrEmpty(textBox.SelectedText))
        {
            SearchTextBox.Text = textBox.SelectedText;
            PerformSearch(textBox.SelectedText);
        }
    }

    private void HideSearchPanel()
    {
        if (SearchPanel == null)
            return;

        SearchPanel.IsVisible = false;
        _searchMatches.Clear();
        _currentMatchIndex = -1;
        _lastSearchText = string.Empty;
        ClearSearchHighlights();

        // Возвращаем фокус на редактор
        NoteContentTextBox?.Focus();
    }

    private void OnCloseSearchClick(object? sender, RoutedEventArgs e)
    {
        HideSearchPanel();
    }

    private void OnSearchTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FindNextMatch();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            HideSearchPanel();
            e.Handled = true;
        }
    }

    private void OnSearchTextBoxTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox searchBox && !string.IsNullOrEmpty(searchBox.Text))
        {
            PerformSearch(searchBox.Text);
        }
        else
        {
            _searchMatches.Clear();
            _currentMatchIndex = -1;
            UpdateSearchResultsText();
        }
    }

    private void OnNextMatchClick(object? sender, RoutedEventArgs e)
    {
        FindNextMatch();
    }

    private void OnPreviousMatchClick(object? sender, RoutedEventArgs e)
    {
        FindPreviousMatch();
    }

    private void PerformSearch(string searchText)
    {
        if (string.IsNullOrEmpty(searchText) || NoteContentTextBox == null)
        {
            _searchMatches.Clear();
            _currentMatchIndex = -1;
            UpdateSearchResultsText();
            ClearSearchHighlights();
            return;
        }

        var textBox = NoteContentTextBox;
        var text = textBox.Text ?? string.Empty;

        _searchMatches.Clear();
        _lastSearchText = searchText;

        // Поиск всех совпадений (без учета регистра)
        int index = 0;
        while (index < text.Length)
        {
            int foundIndex = text.IndexOf(searchText, index, StringComparison.OrdinalIgnoreCase);
            if (foundIndex == -1)
                break;

            _searchMatches.Add(foundIndex);
            index = foundIndex + 1;
        }

        _currentMatchIndex = _searchMatches.Count > 0 ? 0 : -1;
        UpdateSearchResultsText();

        // Обновляем подсветку всех совпадений
        UpdateSearchHighlights();

        if (_currentMatchIndex >= 0)
        {
            HighlightMatch(_currentMatchIndex);
        }
    }

    private void FindNextMatch()
    {
        if (_searchMatches.Count == 0 || SearchTextBox == null || string.IsNullOrEmpty(SearchTextBox.Text))
        {
            if (SearchTextBox != null && !string.IsNullOrEmpty(SearchTextBox.Text))
            {
                PerformSearch(SearchTextBox.Text);
            }
            return;
        }

        _currentMatchIndex = (_currentMatchIndex + 1) % _searchMatches.Count;
        UpdateSearchHighlights();
        HighlightMatch(_currentMatchIndex);
        UpdateSearchResultsText();
    }

    private void FindPreviousMatch()
    {
        if (_searchMatches.Count == 0 || SearchTextBox == null || string.IsNullOrEmpty(SearchTextBox.Text))
        {
            if (SearchTextBox != null && !string.IsNullOrEmpty(SearchTextBox.Text))
            {
                PerformSearch(SearchTextBox.Text);
            }
            return;
        }

        _currentMatchIndex = (_currentMatchIndex - 1 + _searchMatches.Count) % _searchMatches.Count;
        UpdateSearchHighlights();
        HighlightMatch(_currentMatchIndex);
        UpdateSearchResultsText();
    }

    private void HighlightMatch(int matchIndex)
    {
        if (NoteContentTextBox == null || matchIndex < 0 || matchIndex >= _searchMatches.Count || string.IsNullOrEmpty(_lastSearchText))
            return;

        var textBox = NoteContentTextBox;
        int startIndex = _searchMatches[matchIndex];
        int length = _lastSearchText.Length;

        // Убеждаемся, что индексы в пределах текста
        var text = textBox.Text ?? string.Empty;
        if (startIndex < 0 || startIndex >= text.Length)
            return;
        if (startIndex + length > text.Length)
            length = text.Length - startIndex;

        // Выделяем найденный текст
        textBox.SelectionStart = startIndex;
        textBox.SelectionEnd = startIndex + length;
        textBox.CaretIndex = startIndex + length;

        // Возвращаем фокус и прокручиваем к выделенному тексту
        textBox.Focus();

        // Используем отложенное выполнение для прокрутки к выделенному тексту
        Dispatcher.UIThread.Post(() =>
        {
            // Пытаемся прокрутить к выделенному тексту
            // В Avalonia TextBox автоматически прокручивается к курсору при установке CaretIndex
            // Но для надежности можно попробовать вызвать BringIntoView
            try
            {
                textBox.BringIntoView();
            }
            catch
            {
                // Игнорируем ошибки, если метод недоступен
            }
        }, DispatcherPriority.Background);
    }

    private void UpdateSearchResultsText()
    {
        if (SearchResultsText == null)
            return;

        if (_searchMatches.Count == 0)
        {
            SearchResultsText.Text = string.IsNullOrEmpty(_lastSearchText) ? "" : "No matches";
        }
        else
        {
            SearchResultsText.Text = $"{_currentMatchIndex + 1} of {_searchMatches.Count}";
        }
    }

    private void ClearSearchHighlights()
    {
        if (SearchHighlightCanvas == null)
            return;

        SearchHighlightCanvas.Children.Clear();
    }

    private void UpdateSearchHighlights()
    {
        if (SearchHighlightCanvas == null || NoteContentTextBox == null || _searchMatches.Count == 0 || string.IsNullOrEmpty(_lastSearchText))
        {
            ClearSearchHighlights();
            return;
        }

        ClearSearchHighlights();

        // Используем отложенное выполнение для обновления после рендеринга
        // Используем Render для получения координат после полного рендеринга
        Dispatcher.UIThread.Post(() =>
        {
            // Ждем полного рендеринга
            NoteContentTextBox?.InvalidateVisual();
            UpdateSearchHighlightsInternal();
        }, DispatcherPriority.Render);
    }

    private void UpdateSearchHighlightsInternal()
    {
        if (SearchHighlightCanvas == null || NoteContentTextBox == null || _searchMatches.Count == 0 || string.IsNullOrEmpty(_lastSearchText))
            return;

        ClearSearchHighlights();

        var textBox = NoteContentTextBox;
        var text = textBox.Text ?? string.Empty;
        var searchLength = _lastSearchText.Length;

        // Цвета подсветки в стиле приложения (желтый с прозрачностью, гармонирующий с серым фоном #D0D0D0)
        var highlightColor = Color.FromArgb(120, 255, 235, 59); // Светло-желтый для всех совпадений
        var currentHighlightColor = Color.FromArgb(180, 255, 193, 7); // Более яркий желтый для текущего совпадения

        // Сохраняем текущее выделение
        var savedSelectionStart = textBox.SelectionStart;
        var savedSelectionEnd = textBox.SelectionEnd;

        // Используем TextLayout для точных координат с учетом реальных параметров TextBox
        var typeface = new Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight);

        // Получаем реальную ширину TextBox
        var textBoxWidth = textBox.Bounds.Width;
        if (textBoxWidth <= 0 || double.IsInfinity(textBoxWidth) || double.IsNaN(textBoxWidth))
        {
            Dispatcher.UIThread.Post(() => UpdateSearchHighlightsInternal(), DispatcherPriority.Loaded);
            return;
        }

        // Получаем координаты текста через временное выделение первого символа для калибровки
        // Это поможет понять, где начинается текст внутри TextBox
        var calibrationStart = 0;
        textBox.SelectionStart = calibrationStart;
        textBox.SelectionEnd = calibrationStart + 1;
        textBox.UpdateLayout();

        // Получаем координаты через TextLayout с правильной шириной
        // В Avalonia TextBox обычно имеет padding около 2-3 пикселей
        var textPadding = 4.2;
        var maxWidth = textBoxWidth - textPadding * 2;

        var textLayout = new TextLayout(
            text,
            typeface,
            textBox.FontSize,
            textBox.Foreground,
            TextAlignment.Left,
            TextWrapping.Wrap,
            textTrimming: null,
            textDecorations: null,
            maxWidth: maxWidth,
            maxHeight: double.PositiveInfinity);

        // Вычисляем смещение для выравнивания с текстом в TextBox
        // В Avalonia TextBox текст начинается с небольшого отступа от края
        // Обычно это 2-3 пикселя для padding + border
        // Для точной настройки можно менять эти значения:
        var borderThickness = 7.0; // Толщина border TextBox (меняйте для горизонтального смещения)
        var verticalOffset = -2.0; // Вертикальное смещение (меняйте для вертикального смещения)
        var textOffsetX = textPadding + borderThickness;
        var textOffsetY = textPadding + borderThickness + verticalOffset;

        foreach (var (matchIndex, index) in _searchMatches.Select((m, i) => (m, i)))
        {
            if (matchIndex < 0 || matchIndex >= text.Length)
                continue;

            int length = Math.Min(searchLength, text.Length - matchIndex);
            if (length <= 0)
                continue;

            // Получаем координаты начала и конца совпадения
            var startHit = textLayout.HitTestTextPosition(matchIndex);
            var endHit = textLayout.HitTestTextPosition(Math.Min(matchIndex + length, text.Length));

            // Вычисляем размеры прямоугольника подсветки
            var x = startHit.X + textOffsetX;
            var y = startHit.Y + textOffsetY;
            var width = Math.Max(Math.Abs(endHit.X - startHit.X), 8);
            var height = Math.Max(startHit.Height, textBox.FontSize);

            // Создаем прямоугольник подсветки
            var rect = new Rectangle
            {
                Fill = new SolidColorBrush(matchIndex == _searchMatches[_currentMatchIndex]
                    ? currentHighlightColor
                    : highlightColor),
                Width = width,
                Height = height,
                RadiusX = 2,
                RadiusY = 2
            };

            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);

            SearchHighlightCanvas.Children.Add(rect);
        }

        // Восстанавливаем выделение
        textBox.SelectionStart = savedSelectionStart;
        textBox.SelectionEnd = savedSelectionEnd;
    }
}
