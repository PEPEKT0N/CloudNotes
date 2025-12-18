using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    private string? _currentUserEmail;

    public NoteListView()
    {
        InitializeComponent();

        // Получаем сервисы из DI
        _authService = CloudNotes.App.ServiceProvider?.GetService<IAuthService>();
        _syncService = CloudNotes.App.ServiceProvider?.GetService<ISyncService>();

        // Подписываемся на горячие клавиши
        KeyDown += OnKeyDown;

        // Обработчики меню авторизации
        SignInMenuItem.Click += OnSignInMenuItemClick;
        LogoutMenuItem.Click += OnLogoutMenuItemClick;

        // Проверяем состояние авторизации при загрузке
        this.Loaded += async (_, _) => await UpdateAuthMenuAsync();
    }

    private async void OnSignInMenuItemClick(object? sender, RoutedEventArgs e)
    {
        await OpenAuthWindowAsync();
    }

    private async void OnLogoutMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (_authService != null)
        {
            // Останавливаем периодическую синхронизацию перед logout
            _syncService?.StopPeriodicSync();
            
            await _authService.LogoutAsync();
            _currentUserEmail = null;
            await UpdateAuthMenuAsync();
        }
    }

    /// <summary>
    /// Обновить состояние меню авторизации.
    /// </summary>
    private async Task UpdateAuthMenuAsync()
    {
        var isLoggedIn = _authService != null && await _authService.IsLoggedInAsync();

        // Email и разделитель — только когда авторизован
        UserEmailMenuItem.IsVisible = isLoggedIn;
        EmailSeparator.IsVisible = isLoggedIn;

        // Sign in — disabled когда авторизован
        SignInMenuItem.IsEnabled = !isLoggedIn;

        // Sign out — всегда видна, но enabled только когда авторизован
        LogoutMenuItem.IsEnabled = isLoggedIn;

        if (isLoggedIn && !string.IsNullOrEmpty(_currentUserEmail))
        {
            UserEmailMenuItem.Header = $"📧 {_currentUserEmail}";
        }
    }

    private async Task OpenAuthWindowAsync()
    {
        var owner = this.VisualRoot as Window;
        var authWindow = new AuthWindow();

        // Цикл для повторных попыток при ошибках
        while (true)
        {
            var result = await authWindow.ShowDialog<AuthResult?>(owner);

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
                    // Успешная авторизация — сохраняем email и обновляем меню
                    _currentUserEmail = result.Email;
                    await UpdateAuthMenuAsync();
                    
                    // Запускаем периодическую синхронизацию после успешной авторизации
                    _syncService?.StartPeriodicSync();
                    
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
