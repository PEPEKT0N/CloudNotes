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

    public NoteListView()
    {
        InitializeComponent();

        // Получаем AuthService из DI
        _authService = CloudNotes.App.ServiceProvider?.GetService<IAuthService>();

        // Подписываемся на горячие клавиши
        KeyDown += OnKeyDown;

        // Обработчики меню авторизации
        LoginMenuItem.Click += OnLoginMenuItemClick;
        RegisterMenuItem.Click += OnRegisterMenuItemClick;
    }

    private async void OnLoginMenuItemClick(object? sender, RoutedEventArgs e)
    {
        await OpenAuthWindowAsync(showLoginTab: true);
    }

    private async void OnRegisterMenuItemClick(object? sender, RoutedEventArgs e)
    {
        await OpenAuthWindowAsync(showLoginTab: false);
    }

    private async Task OpenAuthWindowAsync(bool showLoginTab)
    {
        var owner = this.VisualRoot as Window;
        var authWindow = new AuthWindow();
        
        // Переключаем на нужную вкладку
        if (!showLoginTab)
        {
            authWindow.SelectRegisterTab();
        }

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
                    // Успешная авторизация
                    System.Diagnostics.Debug.WriteLine($"Auth successful: {result.Email}");
                    // TODO: Обновить UI (показать email в меню, скрыть Login/Register)
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
                var tab = result.IsLogin;
                if (tab)
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
