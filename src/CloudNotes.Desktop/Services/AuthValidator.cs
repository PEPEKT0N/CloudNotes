using System.Text.RegularExpressions;

namespace CloudNotes.Desktop.Services;

/// <summary>
/// Валидация полей авторизации и регистрации.
/// </summary>
public static class AuthValidator
{
    // Константы валидации
    public const int MinPasswordLength = 6;
    public const int MinUsernameLength = 3;
    public const int MaxUsernameLength = 30;

    // Regex для валидации
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex UsernameRegex = new(
        @"^[a-zA-Z0-9_]+$",
        RegexOptions.Compiled);

    /// <summary>
    /// Валидация email.
    /// </summary>
    /// <returns>Сообщение об ошибке или null если валидно.</returns>
    public static string? ValidateEmail(string? email)
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
    /// <returns>Сообщение об ошибке или null если валидно.</returns>
    public static string? ValidateUsername(string? username)
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
    /// <returns>Сообщение об ошибке или null если валидно.</returns>
    public static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return "Password is required";

        if (password.Length < MinPasswordLength)
            return $"Password must be at least {MinPasswordLength} characters";

        return null;
    }

    /// <summary>
    /// Валидация подтверждения пароля.
    /// </summary>
    /// <returns>Сообщение об ошибке или null если валидно.</returns>
    public static string? ValidatePasswordConfirmation(string? password, string? confirmPassword)
    {
        if (password != confirmPassword)
            return "Passwords do not match";

        return null;
    }

    /// <summary>
    /// Полная валидация данных регистрации.
    /// </summary>
    /// <returns>Сообщение об ошибке или null если всё валидно.</returns>
    public static string? ValidateRegistration(string? username, string? email, string? password, string? confirmPassword)
    {
        return ValidateUsername(username)
               ?? ValidateEmail(email)
               ?? ValidatePassword(password)
               ?? ValidatePasswordConfirmation(password, confirmPassword);
    }

    /// <summary>
    /// Полная валидация данных входа.
    /// </summary>
    /// <returns>Сообщение об ошибке или null если всё валидно.</returns>
    public static string? ValidateLogin(string? email, string? password)
    {
        var emailError = ValidateEmail(email);
        if (emailError != null)
            return emailError;

        if (string.IsNullOrWhiteSpace(password))
            return "Password is required";

        return null;
    }
}
