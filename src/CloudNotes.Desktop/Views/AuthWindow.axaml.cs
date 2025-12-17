using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CloudNotes.Desktop.Views
{
    /// <summary>
    /// Окно авторизации с табами Login/Register.
    /// </summary>
    public partial class AuthWindow : Window
    {
        // Константы валидации
        private const int MinPasswordLength = 6;
        private const int MinUsernameLength = 3;
        private const int MaxUsernameLength = 30;
        
        // Regex для валидации
        private static readonly Regex EmailRegex = new(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$", 
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        
        private static readonly Regex UsernameRegex = new(
            @"^[a-zA-Z0-9_]+$", 
            RegexOptions.Compiled);

        /// <summary>
        /// Результат авторизации.
        /// </summary>
        public AuthResult? Result { get; private set; }

        public AuthWindow()
        {
            InitializeComponent();

            this.Opened += OnWindowOpened;

            LoginButton.Click += OnLoginButtonClick;
            RegisterButton.Click += OnRegisterButtonClick;

            // Очистка ошибок при вводе (Login)
            LoginEmailTextBox.TextChanged += (_, _) => ClearLoginError();
            LoginPasswordTextBox.TextChanged += (_, _) => ClearLoginError();

            // Очистка ошибок при вводе (Register)
            RegisterUsernameTextBox.TextChanged += (_, _) => ClearRegisterError();
            RegisterEmailTextBox.TextChanged += (_, _) => ClearRegisterError();
            RegisterPasswordTextBox.TextChanged += (_, _) => ClearRegisterError();

            // Enter для Login
            LoginPasswordTextBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    OnLoginButtonClick(s, e);
                    e.Handled = true;
                }
            };

            // Enter для Register
            RegisterPasswordTextBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    OnRegisterButtonClick(s, e);
                    e.Handled = true;
                }
            };
        }

        private void OnWindowOpened(object? sender, EventArgs e)
        {
            // Фокус на первое поле активной вкладки
            if (AuthTabControl.SelectedIndex == 0)
            {
                LoginEmailTextBox.Focus();
            }
            else
            {
                RegisterUsernameTextBox.Focus();
            }
        }

        private void OnLoginButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var email = LoginEmailTextBox.Text?.Trim();
            var password = LoginPasswordTextBox.Text;

            // Валидация email
            var emailError = ValidateEmail(email);
            if (emailError != null)
            {
                ShowLoginError(emailError);
                LoginEmailTextBox.Focus();
                return;
            }

            // Валидация пароля
            if (string.IsNullOrWhiteSpace(password))
            {
                ShowLoginError("Please enter your password");
                LoginPasswordTextBox.Focus();
                return;
            }

            Result = new AuthResult
            {
                IsLogin = true,
                Email = email!,
                Password = password
            };

            Close(Result);
        }

        private void OnRegisterButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            var username = RegisterUsernameTextBox.Text?.Trim();
            var email = RegisterEmailTextBox.Text?.Trim();
            var password = RegisterPasswordTextBox.Text;

            // Валидация username
            var usernameError = ValidateUsername(username);
            if (usernameError != null)
            {
                ShowRegisterError(usernameError);
                RegisterUsernameTextBox.Focus();
                return;
            }

            // Валидация email
            var emailError = ValidateEmail(email);
            if (emailError != null)
            {
                ShowRegisterError(emailError);
                RegisterEmailTextBox.Focus();
                return;
            }

            // Валидация пароля
            var passwordError = ValidatePassword(password);
            if (passwordError != null)
            {
                ShowRegisterError(passwordError);
                RegisterPasswordTextBox.Focus();
                return;
            }

            Result = new AuthResult
            {
                IsLogin = false,
                UserName = username!,
                Email = email!,
                Password = password!
            };

            Close(Result);
        }

        #region Validation Methods

        /// <summary>
        /// Валидация email.
        /// </summary>
        private static string? ValidateEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return "Email is required";

            if (!EmailRegex.IsMatch(email))
                return "Invalid email format";

            return null;
        }

        /// <summary>
        /// Валидация username.
        /// </summary>
        private static string? ValidateUsername(string? username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return "Username is required";

            if (username.Length < MinUsernameLength)
                return $"Username must be at least {MinUsernameLength} characters";

            if (username.Length > MaxUsernameLength)
                return $"Username must be at most {MaxUsernameLength} characters";

            if (!UsernameRegex.IsMatch(username))
                return "Username can only contain letters, numbers and underscores";

            return null;
        }

        /// <summary>
        /// Валидация пароля.
        /// </summary>
        private static string? ValidatePassword(string? password)
        {
            if (string.IsNullOrWhiteSpace(password))
                return "Password is required";

            if (password.Length < MinPasswordLength)
                return $"Password must be at least {MinPasswordLength} characters";

            return null;
        }

        #endregion

        /// <summary>
        /// Показать ошибку на вкладке Login.
        /// </summary>
        public void ShowLoginError(string message)
        {
            LoginErrorTextBlock.Text = message;
            LoginErrorTextBlock.IsVisible = true;
        }

        /// <summary>
        /// Показать ошибку на вкладке Register.
        /// </summary>
        public void ShowRegisterError(string message)
        {
            RegisterErrorTextBlock.Text = message;
            RegisterErrorTextBlock.IsVisible = true;
        }

        /// <summary>
        /// Скрыть все ошибки.
        /// </summary>
        public void ClearErrors()
        {
            ClearLoginError();
            ClearRegisterError();
        }

        /// <summary>
        /// Скрыть ошибку на вкладке Login.
        /// </summary>
        private void ClearLoginError()
        {
            LoginErrorTextBlock.IsVisible = false;
        }

        /// <summary>
        /// Скрыть ошибку на вкладке Register.
        /// </summary>
        private void ClearRegisterError()
        {
            RegisterErrorTextBlock.IsVisible = false;
        }

        /// <summary>
        /// Переключить на вкладку Register.
        /// </summary>
        public void SelectRegisterTab()
        {
            AuthTabControl.SelectedIndex = 1;
        }

        /// <summary>
        /// Переключить на вкладку Login.
        /// </summary>
        public void SelectLoginTab()
        {
            AuthTabControl.SelectedIndex = 0;
        }

        /// <summary>
        /// Установить значения полей Login.
        /// </summary>
        public void SetLoginFields(string email, string password)
        {
            LoginEmailTextBox.Text = email;
            LoginPasswordTextBox.Text = password;
        }

        /// <summary>
        /// Установить значения полей Register.
        /// </summary>
        public void SetRegisterFields(string username, string email, string password)
        {
            RegisterUsernameTextBox.Text = username;
            RegisterEmailTextBox.Text = email;
            RegisterPasswordTextBox.Text = password;
        }

        /// <summary>
        /// Показать диалог авторизации.
        /// </summary>
        public static Task<AuthResult?> ShowDialogAsync(Window? owner)
        {
            var dialog = new AuthWindow();
            return dialog.ShowDialog<AuthResult?>(owner);
        }
    }

    /// <summary>
    /// Результат авторизации.
    /// </summary>
    public class AuthResult
    {
        /// <summary>
        /// True если Login, False если Register.
        /// </summary>
        public bool IsLogin { get; set; }

        /// <summary>
        /// Username (только для Register).
        /// </summary>
        public string? UserName { get; set; }

        /// <summary>
        /// Email пользователя.
        /// </summary>
        public string Email { get; set; } = null!;

        /// <summary>
        /// Пароль.
        /// </summary>
        public string Password { get; set; } = null!;
    }
}
