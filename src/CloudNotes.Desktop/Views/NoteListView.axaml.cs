using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CloudNotes.Desktop.Services;
using CloudNotes.Desktop.ViewModel;
using CloudNotes.Desktop.Model;
using Microsoft.Extensions.DependencyInjection;
using Refit;

namespace CloudNotes.Desktop.Views;

public partial class NoteListView : UserControl
{
    private readonly IAuthService? _authService;
    private readonly ISyncService? _syncService;
    private readonly INoteServiceFactory? _noteServiceFactory;
    private string? _currentUserEmail;
    private string? _currentUserName;

    public NoteListView()
    {
        InitializeComponent();

        // Получаем сервисы из DI
        _authService = CloudNotes.App.ServiceProvider?.GetService<IAuthService>();
        _syncService = CloudNotes.App.ServiceProvider?.GetService<ISyncService>();
        _noteServiceFactory = CloudNotes.App.ServiceProvider?.GetService<INoteServiceFactory>();

        // Подписываемся на горячие клавиши
        KeyDown += OnKeyDown;

        // Обработчики меню авторизации
        SignInMenuItem.Click += OnSignInMenuItemClick;
        LogoutMenuItem.Click += OnLogoutMenuItemClick;

        // Инициализируем меню авторизации сразу (синхронно) чтобы избежать некорректного состояния
        InitializeAuthMenu();

        // Проверяем состояние авторизации при загрузке и обновляем список заметок
        this.Loaded += async (_, _) =>
        {
            await UpdateAuthMenuAsync();

            // Обновляем список заметок в зависимости от статуса авторизации
            if (DataContext is NotesViewModel viewModel)
            {
                var isLoggedIn = _authService != null && await _authService.IsLoggedInAsync();

                if (isLoggedIn)
                {
                    System.Diagnostics.Debug.WriteLine($"App started with existing session for: {_currentUserEmail}");

                    // Если пользователь уже был авторизован, запускаем синхронизацию
                    if (_syncService != null)
                    {
                        var synced = await _syncService.SyncOnStartupAsync();
                        if (synced)
                        {
                            _syncService.StartPeriodicSync();
                        }
                    }
                }

                await viewModel.RefreshNotesAsync(isLoggedIn: isLoggedIn);
                
                // Обновляем видимость кнопки "Show All"
                ShowAllButton.IsVisible = viewModel.SelectedFolder != null;
            }
        };
    }

    private async void OnSignInMenuItemClick(object? sender, RoutedEventArgs e)
    {
        await OpenAuthWindowAsync();
    }

    private async void OnLogoutMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (_authService != null)
        {
            Console.WriteLine("[Logout] Starting logout process...");

            // Синхронизируем все локальные изменения на сервер ПЕРЕД выходом
            // чтобы не потерять данные пользователя
            if (_syncService != null)
            {
                Console.WriteLine("[Logout] Syncing local changes before logout...");
                try
                {
                    var synced = await _syncService.SyncAsync();
                    Console.WriteLine($"[Logout] Sync completed: {(synced ? "SUCCESS" : "SKIPPED/FAILED")}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Logout] Sync FAILED: {ex.Message}");
                    // Продолжаем logout даже если синхронизация не удалась
                }
            }

            // Останавливаем периодическую синхронизацию
            _syncService?.StopPeriodicSync();

            await _authService.LogoutAsync();
            _currentUserEmail = null;
            _currentUserName = null;
            await UpdateAuthMenuAsync();

            // Переключаемся в гостевой режим
            _noteServiceFactory?.SwitchToGuestMode();

            // Обновляем список заметок - показываем гостевые заметки
            if (DataContext is NotesViewModel viewModel)
            {
                await viewModel.RefreshNotesAsync(isLoggedIn: false);
            }

            Console.WriteLine("[Logout] Logout completed");
        }
    }

    /// <summary>
    /// Инициализировать меню авторизации с правильными значениями по умолчанию.
    /// </summary>
    private void InitializeAuthMenu()
    {
        // По умолчанию считаем пользователя неавторизованным
        // Sign in должна быть активна, Sign out неактивна
        SignInMenuItem.IsEnabled = true;
        LogoutMenuItem.IsEnabled = false;
        UserEmailMenuItem.IsVisible = false;
        EmailSeparator.IsVisible = false;
    }

    /// <summary>
    /// Обновить состояние меню авторизации.
    /// </summary>
    private async Task UpdateAuthMenuAsync()
    {
        bool isLoggedIn = false;

        // Безопасная проверка авторизации с обработкой ошибок
        if (_authService != null)
        {
            try
            {
                isLoggedIn = await _authService.IsLoggedInAsync();

                // Загружаем email и имя пользователя из сохранённых токенов, если пользователь авторизован
                if (isLoggedIn)
                {
                    if (string.IsNullOrEmpty(_currentUserEmail))
                    {
                        _currentUserEmail = await _authService.GetCurrentUserEmailAsync();
                        System.Diagnostics.Debug.WriteLine($"UpdateAuthMenuAsync: Loaded email from tokens: {_currentUserEmail}");
                    }
                    if (string.IsNullOrEmpty(_currentUserName))
                    {
                        _currentUserName = await _authService.GetCurrentUserNameAsync();
                        System.Diagnostics.Debug.WriteLine($"UpdateAuthMenuAsync: Loaded username from tokens: {_currentUserName}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"UpdateAuthMenuAsync: isLoggedIn = {isLoggedIn}, email = {_currentUserEmail}, username = {_currentUserName}");
            }
            catch (Exception ex)
            {
                // При ошибке считаем неавторизованным
                isLoggedIn = false;
                System.Diagnostics.Debug.WriteLine($"UpdateAuthMenuAsync: Error checking auth status: {ex.Message}");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("UpdateAuthMenuAsync: _authService is null");
        }

        // Обновляем UI в UI потоке для безопасности
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Email и разделитель — только когда авторизован
            UserEmailMenuItem.IsVisible = isLoggedIn;
            EmailSeparator.IsVisible = isLoggedIn;

            // Sign in — активна когда НЕ авторизован
            SignInMenuItem.IsEnabled = !isLoggedIn;

            // Sign out — активна только когда авторизован
            LogoutMenuItem.IsEnabled = isLoggedIn;

            System.Diagnostics.Debug.WriteLine(
                $"UpdateAuthMenuAsync: SignInMenuItem.IsEnabled = {!isLoggedIn}, LogoutMenuItem.IsEnabled = {isLoggedIn}");

            if (isLoggedIn)
            {
                // Отображаем имя пользователя вместо email
                var displayName = !string.IsNullOrEmpty(_currentUserName) ? _currentUserName :
                                 (!string.IsNullOrEmpty(_currentUserEmail) ? _currentUserEmail : "Unknown user");
                UserEmailMenuItem.Header = $"👤 {displayName}";
            }
        });
    }

    private async Task OpenAuthWindowAsync()
    {
        var owner = this.VisualRoot as Window;
        var authWindow = new AuthWindow();

        // Цикл для повторных попыток при ошибках
        while (true)
        {
            var result = await authWindow.ShowDialog<AuthResult?>(owner!);

            if (result == null)
            {
                // Пользователь закрыл окно
                break;
            }

            if (_authService == null)
            {
                System.Diagnostics.Debug.WriteLine("AuthService is not available");
                break;
            }

            try
            {
                bool success;
                if (result.IsLogin)
                {
                    success = await _authService.LoginAsync(result.Email, result.Password);
                }
                else
                {
                    success = await _authService.RegisterAsync(result.UserName!, result.Email, result.Password);
                }

                if (success)
                {
                    // Проверяем, это тот же пользователь или другой
                    // Используем GetLastLoggedInEmail() - он сохраняется даже после logout
                    var previousEmail = _authService.GetLastLoggedInEmail();
                    var isSameUser = !string.IsNullOrEmpty(previousEmail) &&
                                     string.Equals(previousEmail, result.Email, StringComparison.OrdinalIgnoreCase);

                    Console.WriteLine($"[Auth] Last user: {previousEmail ?? "null"}, New user: {result.Email}, IsSameUser: {isSameUser}");

                    // Успешная авторизация — сохраняем email и имя пользователя, обновляем меню
                    _currentUserEmail = result.Email;
                    _currentUserName = await _authService.GetCurrentUserNameAsync();
                    await UpdateAuthMenuAsync();

                    // Очищаем локальную БД только если это ДРУГОЙ пользователь
                    // Если тот же пользователь - сохраняем его локальные данные
                    if (_noteServiceFactory != null && !isSameUser)
                    {
                        // Если был предыдущий пользователь, сначала синхронизируем его изменения
                        // (на случай если он не вышел нормально)
                        if (!string.IsNullOrEmpty(previousEmail) && _syncService != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Syncing previous user ({previousEmail}) changes before clearing...");
                            try
                            {
                                // Примечание: эта синхронизация может не сработать если токены 
                                // предыдущего пользователя уже недействительны
                                await _syncService.SyncAsync();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Could not sync previous user's changes: {ex.Message}");
                            }
                        }

                        try
                        {
                            await _noteServiceFactory.ClearLocalDatabaseAsync();
                            System.Diagnostics.Debug.WriteLine($"Local database cleared (different user: {previousEmail ?? "guest"} -> {result.Email})");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error clearing local database: {ex.Message}");
                        }
                    }
                    else if (isSameUser)
                    {
                        System.Diagnostics.Debug.WriteLine($"Same user ({result.Email}) - keeping local data");
                    }

                    // Запускаем синхронизацию после успешной авторизации
                    if (_syncService != null)
                    {
                        // Выполняем синхронизацию один раз для загрузки заметок с сервера
                        await _syncService.SyncOnStartupAsync();
                        // Запускаем периодическую синхронизацию
                        _syncService.StartPeriodicSync();
                    }

                    // Обновляем список заметок после синхронизации
                    if (DataContext is NotesViewModel vm)
                    {
                        await vm.RefreshNotesAsync(isLoggedIn: true);
                    }

                    System.Diagnostics.Debug.WriteLine($"Auth successful: {result.Email}");
                    break;
                }
            }
            catch (ApiException apiEx)
            {
                // Обработка ошибок от сервера
                var errorMessage = ParseApiError(apiEx);

                // Создаём новое окно для повторной попытки
                authWindow = new AuthWindow();
                if (result.IsLogin)
                {
                    authWindow.SelectLoginTab();
                    authWindow.SetLoginFields(result.Email, string.Empty);
                    authWindow.ShowLoginError(errorMessage);
                }
                else
                {
                    authWindow.SelectRegisterTab();
                    authWindow.SetRegisterFields(result.UserName!, result.Email, string.Empty);
                    authWindow.ShowRegisterError(errorMessage);
                }
                continue;
            }
            catch (HttpRequestException)
            {
                // Сетевая ошибка
                authWindow = new AuthWindow();
                if (result.IsLogin)
                {
                    authWindow.SelectLoginTab();
                    authWindow.SetLoginFields(result.Email, string.Empty);
                    authWindow.ShowLoginError("Connection error. Please check your internet connection.");
                }
                else
                {
                    authWindow.SelectRegisterTab();
                    authWindow.SetRegisterFields(result.UserName!, result.Email, string.Empty);
                    authWindow.ShowRegisterError("Connection error. Please check your internet connection.");
                }
                continue;
            }
            catch (Exception ex)
            {
                // Неизвестная ошибка
                System.Diagnostics.Debug.WriteLine($"Auth error: {ex}");
                authWindow = new AuthWindow();
                if (result.IsLogin)
                {
                    authWindow.SelectLoginTab();
                    authWindow.ShowLoginError("An unexpected error occurred. Please try again.");
                }
                else
                {
                    authWindow.SelectRegisterTab();
                    authWindow.ShowRegisterError("An unexpected error occurred. Please try again.");
                }
                continue;
            }
        }
    }

    /// <summary>
    /// Парсинг ошибки от API.
    /// </summary>
    private static string ParseApiError(ApiException apiEx)
    {
        // Пытаемся извлечь сообщение об ошибке из ответа API
        string? errorMessage = null;
        try
        {
            if (!string.IsNullOrEmpty(apiEx.Content))
            {
                var errorObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(apiEx.Content);
                if (errorObj.TryGetProperty("error", out var errorProp))
                {
                    errorMessage = errorProp.GetString();
                }
                else if (errorObj.TryGetProperty("errors", out var errorsProp))
                {
                    var errors = errorsProp.EnumerateArray().ToList();
                    errorMessage = string.Join(", ", errors.Select(e => e.GetString()));
                }
            }
        }
        catch
        {
            // Игнорируем ошибки парсинга
        }

        if (!string.IsNullOrEmpty(errorMessage))
        {
            return errorMessage;
        }

        return apiEx.StatusCode switch
        {
            HttpStatusCode.BadRequest => "Invalid request. Please check your input.",
            HttpStatusCode.Unauthorized => "Invalid email or password.",
            HttpStatusCode.Conflict => "This email is already registered.",
            HttpStatusCode.NotFound => "User not found.",
            HttpStatusCode.InternalServerError => "Server error. Please try again later.",
            _ => apiEx.Message ?? "An error occurred. Please try again."
        };
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

            // Ctrl+S — сохранить
            case Key.S when ctrl:
                await SaveNotesAsync(vm);
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

    private async Task SaveNotesAsync(NotesViewModel vm)
    {
        if (vm.SelectedNote != null)
        {
            await vm.SaveNoteAsync(vm.SelectedNote);
        }
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

    private void OnFolderSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Фильтрация заметок по выбранной папке обрабатывается в ViewModel через SelectedFolder property
        if (DataContext is NotesViewModel vm)
        {
            // Явно синхронизируем SelectedFolder с выбором в TreeView
            var selectedItem = FolderTreeView.SelectedItem as FolderTreeItem;
            if (vm.SelectedFolder != selectedItem)
            {
                vm.SelectedFolder = selectedItem;
            }
            
            // Обновляем видимость кнопки "Show All"
            ShowAllButton.IsVisible = vm.SelectedFolder != null;
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
