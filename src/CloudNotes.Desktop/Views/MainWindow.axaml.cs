using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
    private ISyncService? _syncService;
    private List<int> _searchMatches = new List<int>();
    private int _currentMatchIndex = -1;
    private string _lastSearchText = string.Empty;

    // Сохраняем выделение текста перед потерей фокуса
    private int _savedSelectionStart = -1;
    private int _savedSelectionEnd = -1;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new NotesViewModel();
        DataContext = _viewModel;

        NoteListViewControl.DataContext = _viewModel;

        // Получаем сервисы из DI
        _conflictService = App.ServiceProvider?.GetService<IConflictService>();
        _authService = App.ServiceProvider?.GetService<IAuthService>();
        _syncService = App.ServiceProvider?.GetService<ISyncService>();

        if (_conflictService != null)
        {
            _conflictService.ConflictDetected += OnConflictDetected;
        }

        // При активации окна (вернулись в приложение) — синхронизация, чтобы подтянуть изменения с другого устройства
        Activated += OnWindowActivated;

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

    private async void OnWindowActivated(object? sender, EventArgs e)
    {
        if (_syncService == null || _authService == null) return;
        var isLoggedIn = await _authService.IsLoggedInAsync();
        if (!isLoggedIn) return;
        _ = Task.Run(async () =>
        {
            await _syncService.SyncAsync();
            await Dispatcher.UIThread.InvokeAsync(async () => await _viewModel.RefreshNotesAsync(isLoggedIn: true));
        });
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

        // Ctrl+S — сохранить заметку (работает и когда фокус не в редакторе)
        if (e.Key == Key.S && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            if (_viewModel.SelectedNote != null)
            {
                if (NoteContentTextBox != null)
                {
                    var text = NoteContentTextBox.Text ?? string.Empty;
                    _viewModel.SelectedNote.Content = text;
                }
                _ = _viewModel.SaveNoteAsync(_viewModel.SelectedNote);
            }
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
    private async void OnNoteContentKeyDown(object? sender, KeyEventArgs e)
    {
        // Логируем все нажатия клавиш для отладки
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            System.Diagnostics.Debug.WriteLine($"KeyDown: Ctrl+{e.Key}");
        }

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        switch (e.Key)
        {
            case Key.S:
                // Ctrl+S — сохранить заметку (содержимое из редактора в модель и в БД)
                if (NoteContentTextBox != null && _viewModel.SelectedNote != null)
                {
                    var text = NoteContentTextBox.Text ?? string.Empty;
                    _viewModel.SelectedNote.Content = text;
                    await _viewModel.SaveNoteAsync(_viewModel.SelectedNote);
                }
                e.Handled = true;
                break;
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
            case Key.V:
                // Обработка Ctrl+V для вставки картинок
                System.Diagnostics.Debug.WriteLine("=== Ctrl+V pressed ===");
                System.Diagnostics.Debug.WriteLine($"SelectedNote: {_viewModel.SelectedNote != null}");
                System.Diagnostics.Debug.WriteLine($"IsPreviewMode: {_viewModel.IsPreviewMode}");

                var handled = await HandleImagePasteAsync();
                e.Handled = handled;

                if (handled)
                {
                    System.Diagnostics.Debug.WriteLine("✓ Image pasted successfully, preventing default paste");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("✗ No image found or paste failed, allowing default text paste");
                }
                break;
        }
    }


    /// <summary>
    /// Сохраняет выделение текста перед потерей фокуса (когда пользователь кликает на кнопку).
    /// </summary>
    private void OnNoteContentLostFocus(object? sender, RoutedEventArgs e)
    {
        var textBox = NoteContentTextBox;
        if (textBox != null)
        {
            _savedSelectionStart = textBox.SelectionStart;
            _savedSelectionEnd = textBox.SelectionEnd;
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

    private void OnStrikethroughButtonClick(object? sender, RoutedEventArgs e)
    {
        // Сохраняем выделение перед применением форматирования
        // так как при клике на кнопку TextBox может потерять фокус
        var textBox = NoteContentTextBox;
        if (textBox != null && textBox.IsFocused)
        {
            // Если TextBox в фокусе, применяем форматирование напрямую
            ApplyStrikethroughFormatting();
        }
        else
        {
            // Если TextBox не в фокусе, возвращаем фокус и применяем форматирование
            if (textBox != null)
            {
                textBox.Focus();
                // Используем Dispatcher для применения форматирования после возврата фокуса
                Dispatcher.UIThread.Post(() =>
                {
                    ApplyStrikethroughFormatting();
                }, DispatcherPriority.Normal);
            }
        }
    }

    private void OnSpoilerButtonClick(object? sender, RoutedEventArgs e)
    {
        ApplySpoilerFormatting();
    }

    private void OnUnorderedListButtonClick(object? sender, RoutedEventArgs e)
    {
        InsertListMarker("- ");
    }

    private void OnOrderedListButtonClick(object? sender, RoutedEventArgs e)
    {
        InsertListMarker("1. ");
    }

    private async void OnTableButtonClick(object? sender, RoutedEventArgs e)
    {
        await InsertTableAsync();
    }

    private async void OnImageButtonClick(object? sender, RoutedEventArgs e)
    {
        await InsertImageFromFileAsync();
    }

    private void OnLinkButtonClick(object? sender, RoutedEventArgs e)
    {
        InsertLink();
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
    /// Применяет зачеркивание ~~текст~~ к выделенному тексту.
    /// </summary>
    private void ApplyStrikethroughFormatting()
    {
        WrapSelectedText("~~", "~~");
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
    /// Вставляет маркер списка в начало текущей строки или создает новую строку со списком.
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
    private void InsertListMarker(string marker)
    {
        if (_viewModel.SelectedNote == null || _viewModel.IsPreviewMode)
            return;

        var textBox = NoteContentTextBox;
        if (textBox == null)
            return;

        var currentText = textBox.Text ?? string.Empty;
        var caretIndex = Math.Min(textBox.CaretIndex, currentText.Length);
        if (caretIndex < 0) caretIndex = 0;

        // Находим начало текущей строки
        var lineStart = caretIndex;
        while (lineStart > 0 && currentText[lineStart - 1] != '\n')
        {
            lineStart--;
        }

        // Проверяем, есть ли уже маркер списка в начале строки
        var lineEnd = currentText.IndexOf('\n', lineStart);
        if (lineEnd == -1) lineEnd = currentText.Length;
        var lineText = currentText.Substring(lineStart, lineEnd - lineStart);
        var trimmedLine = lineText.TrimStart();

        var numberedListRegex = new System.Text.RegularExpressions.Regex(@"^\d+\.\s");

        if (trimmedLine.StartsWith("- ") || trimmedLine.StartsWith("* "))
        {
            // Уже есть маркер маркированного списка - не добавляем
            return;
        }

        // Для нумерованных списков проверяем и определяем следующий номер
        if (marker == "1. ")
        {
            if (numberedListRegex.IsMatch(trimmedLine))
            {
                // Уже есть маркер нумерованного списка - не добавляем
                return;
            }

            // Находим предыдущие строки с нумерованными списками для определения следующего номера
            var lines = currentText.Substring(0, lineStart).Split('\n');
            int maxNumber = 0;
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var trimmed = lines[i].TrimStart();
                var match = numberedListRegex.Match(trimmed);
                if (match.Success)
                {
                    var numberStr = match.Value.TrimEnd('.', ' ');
                    if (int.TryParse(numberStr, out int number))
                    {
                        maxNumber = Math.Max(maxNumber, number);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    // Прерываем поиск, если встретили непустую строку без маркера списка
                    break;
                }
            }

            // Используем следующий номер
            marker = $"{maxNumber + 1}. ";
        }
        else if (numberedListRegex.IsMatch(trimmedLine))
        {
            // Уже есть маркер нумерованного списка - не добавляем
            return;
        }

        // Вставляем маркер в начало строки
        var newText = currentText.Insert(lineStart, marker);
        textBox.Text = newText;

        // Перемещаем курсор после маркера
        textBox.CaretIndex = lineStart + marker.Length;

        // Возвращаем фокус на TextBox
        textBox.Focus();
    }

    /// <summary>
    /// Обрабатывает вставку картинки из буфера обмена (Ctrl+V).
    /// </summary>
    private async System.Threading.Tasks.Task<bool> HandleImagePasteAsync()
    {
        System.Diagnostics.Debug.WriteLine("=== HandleImagePasteAsync START ===");
        System.Console.WriteLine("=== HandleImagePasteAsync START ==="); // Также в консоль для видимости

        if (_viewModel.SelectedNote == null)
        {
            System.Diagnostics.Debug.WriteLine("No note selected");
            System.Console.WriteLine("No note selected");
            return false;
        }

        if (_viewModel.IsPreviewMode)
        {
            System.Diagnostics.Debug.WriteLine("Preview mode active");
            System.Console.WriteLine("Preview mode active");
            return false;
        }

        var textBox = NoteContentTextBox;
        if (textBox == null)
        {
            System.Diagnostics.Debug.WriteLine("TextBox is null");
            return false;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine("Getting clipboard...");
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                System.Diagnostics.Debug.WriteLine("TopLevel is null");
                return false;
            }

            if (topLevel.Clipboard == null)
            {
                System.Diagnostics.Debug.WriteLine("Clipboard is null");
                return false;
            }

            var clipboard = topLevel.Clipboard;
            System.Diagnostics.Debug.WriteLine("Clipboard obtained");

            // Проверяем, есть ли в буфере обмена изображение
            var formats = await clipboard.GetFormatsAsync();
            if (formats == null)
            {
                System.Diagnostics.Debug.WriteLine("GetFormatsAsync returned null");
                System.Console.WriteLine("GetFormatsAsync returned null");
                return false;
            }

            // Логируем доступные форматы для отладки
            System.Diagnostics.Debug.WriteLine($"Available clipboard formats: {string.Join(", ", formats)}");
            System.Console.WriteLine($"Available clipboard formats: {string.Join(", ", formats)}");

            Avalonia.Media.Imaging.Bitmap? bitmap = null;

            // Пробуем получить изображение разными способами
            // 1. Пробуем получить как Bitmap напрямую (если Avalonia поддерживает)
            try
            {
                // В Avalonia может быть специальный метод для изображений
                // Пробуем получить через стандартные форматы
                var imageFormats = new[] { "image/png", "image/jpeg", "image/jpg", "image/bmp", "image/gif",
                                           "PNG", "JFIF", "DeviceIndependentBitmap", "CF_DIB", "CF_BITMAP" };

                foreach (var format in imageFormats)
                {
                    if (!formats.Contains(format))
                        continue;

                    System.Diagnostics.Debug.WriteLine($"Trying format: {format}");
                    System.Console.WriteLine($"Trying format: {format}");

                    try
                    {
                        var imageData = await clipboard.GetDataAsync(format);

                        if (imageData == null)
                            continue;

                        System.Diagnostics.Debug.WriteLine($"Got data for format {format}, type: {imageData.GetType().Name}");
                        System.Console.WriteLine($"Got data for format {format}, type: {imageData.GetType().Name}");

                        // Пробуем разные способы получения Bitmap
                        if (imageData is Avalonia.Media.Imaging.Bitmap directBitmap)
                        {
                            bitmap = directBitmap;
                            System.Diagnostics.Debug.WriteLine("Got direct Bitmap");
                            break;
                        }
                        else if (imageData is System.IO.Stream stream)
                        {
                            // Важно: не закрываем stream, так как он может быть использован позже
                            stream.Position = 0;
                            bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
                            System.Diagnostics.Debug.WriteLine("Created Bitmap from Stream");
                            break;
                        }
                        else if (imageData is byte[] bytes)
                        {
                            using var memoryStream = new System.IO.MemoryStream(bytes);
                            bitmap = new Avalonia.Media.Imaging.Bitmap(memoryStream);
                            System.Diagnostics.Debug.WriteLine("Created Bitmap from byte[]");
                            break;
                        }
                    }
                    catch (Exception formatEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error getting data for format {format}: {formatEx.Message}");
                        continue; // Пробуем следующий формат
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in image format loop: {ex.Message}");
                System.Console.WriteLine($"Error in image format loop: {ex.Message}");
            }

            if (bitmap == null)
            {
                System.Diagnostics.Debug.WriteLine("Failed to create bitmap from clipboard");
                System.Console.WriteLine("Failed to create bitmap from clipboard");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"Bitmap created: {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");

            // Сохраняем изображение и вставляем ссылку
            await InsertImageFromBitmapAsync(bitmap);
            System.Diagnostics.Debug.WriteLine("=== HandleImagePasteAsync SUCCESS ===");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== HandleImagePasteAsync ERROR ===");
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Открывает диалог выбора файла изображения и вставляет его в текст.
    /// </summary>
    private async System.Threading.Tasks.Task InsertImageFromFileAsync()
    {
        if (_viewModel.SelectedNote == null || _viewModel.IsPreviewMode)
            return;

        var textBox = NoteContentTextBox;
        if (textBox == null)
            return;

        try
        {
            // Используем новый API StorageProvider вместо устаревшего OpenFileDialog
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.StorageProvider == null)
            {
                System.Diagnostics.Debug.WriteLine("StorageProvider not available");
                return;
            }

            var filePickerOptions = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Select Image",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("Images")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp" }
                    }
                }
            };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(filePickerOptions);
            if (files == null || files.Count == 0)
                return;

            var file = files[0];
            var imagePath = file.Path.LocalPath;
            await InsertImageFromPathAsync(imagePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error inserting image from file: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Вставляет изображение из локального файла в текст как ссылку (название без расширения, путь к файлу).
    /// </summary>
    private async System.Threading.Tasks.Task InsertImageFromPathAsync(string imagePath)
    {
        if (_viewModel.SelectedNote == null || _viewModel.IsPreviewMode)
            return;

        var textBox = NoteContentTextBox;
        if (textBox == null)
            return;

        try
        {
            // Название файла без расширения — для подписи в Markdown ![название](url)
            var nameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(imagePath);
            if (string.IsNullOrWhiteSpace(nameWithoutExtension))
                nameWithoutExtension = "Image";

            // Ссылка на локальный файл (file:// с прямыми слэшами для совместимости)
            var fileUrl = "file:///" + imagePath.Replace('\\', '/');

            // Вставляем Markdown: подпись без расширения, ссылка на локальный файл
            var imageMarkdown = $"![{nameWithoutExtension}]({fileUrl})";

            var currentText = textBox.Text ?? string.Empty;
            var caretIndex = Math.Min(textBox.CaretIndex, currentText.Length);
            if (caretIndex < 0) caretIndex = 0;

            // Добавляем переносы строк если нужно
            if (caretIndex > 0 && currentText[caretIndex - 1] != '\n')
            {
                imageMarkdown = "\n" + imageMarkdown;
            }
            imageMarkdown += "\n";

            var newText = currentText.Insert(caretIndex, imageMarkdown);
            textBox.Text = newText;

            // Обновляем Content заметки напрямую, чтобы изменения сохранились
            if (_viewModel.SelectedNote != null)
            {
                _viewModel.SelectedNote.Content = newText;
                System.Diagnostics.Debug.WriteLine("Note content updated in ViewModel (from path)");
            }

            textBox.CaretIndex = caretIndex + imageMarkdown.Length;
            textBox.Focus();
            System.Diagnostics.Debug.WriteLine($"Image inserted successfully from path: {imagePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error inserting image from path: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Вставляет изображение из Bitmap в текст как base64.
    /// </summary>
    private async System.Threading.Tasks.Task InsertImageFromBitmapAsync(Avalonia.Media.Imaging.Bitmap bitmap)
    {
        System.Diagnostics.Debug.WriteLine("=== InsertImageFromBitmapAsync START ===");

        if (_viewModel.SelectedNote == null)
        {
            System.Diagnostics.Debug.WriteLine("No note selected in InsertImageFromBitmapAsync");
            return;
        }

        if (_viewModel.IsPreviewMode)
        {
            System.Diagnostics.Debug.WriteLine("Preview mode in InsertImageFromBitmapAsync");
            return;
        }

        var textBox = NoteContentTextBox;
        if (textBox == null)
        {
            System.Diagnostics.Debug.WriteLine("TextBox is null in InsertImageFromBitmapAsync");
            return;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"Bitmap size: {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
            System.Diagnostics.Debug.WriteLine("Converting bitmap to base64...");

            // Конвертируем Bitmap в PNG bytes
            // В Avalonia Bitmap.Save принимает Stream и сохраняет в PNG
            byte[] imageBytes;
            try
            {
                using (var memoryStream = new System.IO.MemoryStream())
                {
                    // Сохраняем Bitmap в PNG формат
                    bitmap.Save(memoryStream);
                    memoryStream.Position = 0; // Сбрасываем позицию потока
                    imageBytes = memoryStream.ToArray();
                }

                if (imageBytes == null || imageBytes.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to convert bitmap to bytes - empty result");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Successfully converted bitmap to {imageBytes.Length} bytes");
            }
            catch (Exception saveEx)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving bitmap: {saveEx.Message}");
                System.Diagnostics.Debug.WriteLine($"Type: {saveEx.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"Stack: {saveEx.StackTrace}");
                return;
            }

            var base64String = Convert.ToBase64String(imageBytes);
            System.Diagnostics.Debug.WriteLine($"Base64 length: {base64String.Length}");

            // Вставляем Markdown изображение с base64
            var imageMarkdown = $"![Image](data:image/png;base64,{base64String})";

            var currentText = textBox.Text ?? string.Empty;
            var caretIndex = Math.Min(textBox.CaretIndex, currentText.Length);
            if (caretIndex < 0) caretIndex = 0;

            // Добавляем переносы строк если нужно
            if (caretIndex > 0 && currentText[caretIndex - 1] != '\n')
            {
                imageMarkdown = "\n" + imageMarkdown;
            }
            imageMarkdown += "\n";

            System.Diagnostics.Debug.WriteLine($"Inserting markdown at position {caretIndex}, length: {imageMarkdown.Length}");
            var newText = currentText.Insert(caretIndex, imageMarkdown);

            // Обновляем текст в TextBox
            textBox.Text = newText;

            // Обновляем Content заметки напрямую, чтобы изменения сохранились
            if (_viewModel.SelectedNote != null)
            {
                _viewModel.SelectedNote.Content = newText;
                System.Diagnostics.Debug.WriteLine("Note content updated in ViewModel");
            }

            textBox.CaretIndex = caretIndex + imageMarkdown.Length;
            textBox.Focus();
            System.Diagnostics.Debug.WriteLine("=== InsertImageFromBitmapAsync SUCCESS ===");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"=== InsertImageFromBitmapAsync ERROR ===");
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Вставляет ссылку в текст. Если есть выделенный текст, оборачивает его в ссылку.
    /// </summary>
    private void InsertLink()
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

        if (!string.IsNullOrEmpty(selectedText) && selectionLength > 0)
        {
            // Есть выделенный текст - оборачиваем в ссылку
            var beforeSelection = currentText.Substring(0, selectionStart);
            var afterSelection = currentText.Substring(selectionStart + selectionLength);

            var linkMarkdown = $"[{selectedText}](url)";
            var newText = beforeSelection + linkMarkdown + afterSelection;
            textBox.Text = newText;

            // Выделяем URL для редактирования
            var urlStart = selectionStart + selectedText.Length + 3; // 3 = длина "["
            textBox.SelectionStart = urlStart;
            textBox.SelectionEnd = urlStart + 3; // 3 = длина "url"
        }
        else
        {
            // Нет выделения - вставляем шаблон ссылки
            var caretIndex = Math.Min(textBox.CaretIndex, currentText.Length);
            if (caretIndex < 0) caretIndex = 0;

            var linkMarkdown = "[Link text](url)";
            var newText = currentText.Insert(caretIndex, linkMarkdown);
            textBox.Text = newText;

            // Выделяем "Link text" для редактирования
            textBox.SelectionStart = caretIndex + 1;
            textBox.SelectionEnd = caretIndex + 10; // 10 = длина "Link text"
        }

        textBox.Focus();
    }

    /// <summary>
    /// Открывает диалог выбора размерности таблицы и вставляет таблицу в текст.
    /// </summary>
    private async System.Threading.Tasks.Task InsertTableAsync()
    {
        if (_viewModel.SelectedNote == null || _viewModel.IsPreviewMode)
            return;

        var textBox = NoteContentTextBox;
        if (textBox == null)
            return;

        // Открываем диалог выбора размерности
        var dialog = new TableSizeDialog();
        await dialog.ShowDialog(this);

        if (!dialog.IsInserted)
            return;

        var rows = dialog.Rows;
        var columns = dialog.Columns;

        // Генерируем Markdown таблицу
        var tableMarkdown = GenerateTableMarkdown(rows, columns);

        var currentText = textBox.Text ?? string.Empty;
        var caretIndex = Math.Min(textBox.CaretIndex, currentText.Length);
        if (caretIndex < 0) caretIndex = 0;

        // Добавляем перенос строки перед таблицей, если нужно
        if (caretIndex > 0 && currentText[caretIndex - 1] != '\n')
        {
            tableMarkdown = "\n" + tableMarkdown;
        }

        // Добавляем перенос строки после таблицы
        tableMarkdown += "\n";

        // Вставляем таблицу в позицию курсора
        var newText = currentText.Insert(caretIndex, tableMarkdown);
        textBox.Text = newText;

        // Перемещаем курсор после вставленной таблицы
        textBox.CaretIndex = caretIndex + tableMarkdown.Length;

        // Возвращаем фокус на TextBox
        textBox.Focus();
    }

    /// <summary>
    /// Генерирует Markdown таблицу заданной размерности.
    /// </summary>
    private string GenerateTableMarkdown(int rows, int columns)
    {
        var sb = new System.Text.StringBuilder();

        // Заголовок таблицы
        for (int col = 0; col < columns; col++)
        {
            sb.Append("| Header ");
            sb.Append(col + 1);
            sb.Append(" ");
        }
        sb.AppendLine("|");

        // Разделитель заголовка
        for (int col = 0; col < columns; col++)
        {
            sb.Append("| --- ");
        }
        sb.AppendLine("|");

        // Строки таблицы
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                sb.Append("| Cell ");
                sb.Append(row + 1);
                sb.Append(",");
                sb.Append(col + 1);
                sb.Append(" ");
            }
            sb.AppendLine("|");
        }

        return sb.ToString();
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

        // Используем сохраненное выделение, если оно есть, иначе текущее
        int selectionStart;
        int selectionEnd;
        int selectionLength;
        string selectedText;

        if (_savedSelectionStart >= 0 && _savedSelectionEnd > _savedSelectionStart)
        {
            // Используем сохраненное выделение (когда пользователь кликнул на кнопку)
            selectionStart = _savedSelectionStart;
            selectionEnd = _savedSelectionEnd;
            selectionLength = selectionEnd - selectionStart;

            // Проверяем границы сохраненного выделения
            if (selectionStart > currentText.Length)
                selectionStart = currentText.Length;
            if (selectionEnd > currentText.Length)
                selectionEnd = currentText.Length;
            selectionLength = selectionEnd - selectionStart;

            selectedText = selectionLength > 0 && selectionStart < currentText.Length
                ? currentText.Substring(selectionStart, selectionLength)
                : string.Empty;

            // Сбрасываем сохраненное выделение после использования
            _savedSelectionStart = -1;
            _savedSelectionEnd = -1;
        }
        else
        {
            // Используем текущее выделение (когда используется горячая клавиша)
            selectionStart = textBox.SelectionStart;
            selectionEnd = textBox.SelectionEnd;
            selectionLength = selectionEnd - selectionStart;

            selectedText = selectionLength > 0 && selectionStart < currentText.Length
                ? currentText.Substring(selectionStart, Math.Min(selectionLength, currentText.Length - selectionStart))
                : string.Empty;
        }

        // Проверка границ
        if (selectionStart < 0)
            selectionStart = 0;
        if (selectionStart > currentText.Length)
            selectionStart = currentText.Length;
        if (selectionEnd > currentText.Length)
            selectionEnd = currentText.Length;
        selectionLength = selectionEnd - selectionStart;

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

    /// <summary>
    /// Обработчик нажатия на кнопку вставки шаблона карточки.
    /// </summary>
    private void OnFlashcardButtonClick(object? sender, RoutedEventArgs e)
    {
        InsertFlashcardTemplate();
    }

    /// <summary>
    /// Обработчик нажатия на кнопку "Study" - изучение карточек из текущей заметки.
    /// </summary>
    private async void OnStudyButtonClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedNote == null)
            return;

        var flashcards = FlashcardParser.Parse(_viewModel.SelectedNote.Content);
        if (flashcards.Count == 0)
            return;

        var userEmail = _authService != null && await _authService.IsLoggedInAsync()
            ? await _authService.GetCurrentUserEmailAsync()
            : null;

        await StudyDialog.ShowDialogAsync(this, flashcards, _viewModel.SelectedNote.Id, userEmail);
    }

    /// <summary>
    /// Обработчик нажатия на кнопку "Study by Tags" - изучение карточек из заметок с выбранными тегами.
    /// </summary>
    private async void OnStudyAllButtonClick(object? sender, RoutedEventArgs e)
    {
        var userEmail = _authService != null && await _authService.IsLoggedInAsync()
            ? await _authService.GetCurrentUserEmailAsync()
            : null;

        var (isConfirmed, selectedTagIds) = await TagSelectionDialog.ShowDialogAsync(this, userEmail);
        if (!isConfirmed || selectedTagIds.Count == 0)
            return;

        var tagService = App.ServiceProvider?.GetService<ITagService>();
        if (tagService == null)
            return;

        var cards = await tagService.GetFlashcardsByTagsAsync(selectedTagIds);
        if (cards == null || cards.Count == 0)
            return;

        await StudyDialog.ShowDialogByTagsAsync(this, cards, userEmail);
    }
}
