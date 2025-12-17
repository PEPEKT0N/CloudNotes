using CloudNotes.Desktop.Services;
using Xunit;

namespace CloudNotes.Desktop.Tests;

/// <summary>
/// Тесты валидации регистрации и авторизации.
/// </summary>
public class AuthValidatorTests
{
    #region Positive Tests

    [Fact]
    public void ValidateRegistration_WithValidData_ReturnsNull()
    {
        // Arrange
        var username = "validuser";
        var email = "test@example.com";
        var password = "password123";
        var confirmPassword = "password123";

        // Act
        var result = AuthValidator.ValidateRegistration(username, email, password, confirmPassword);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ValidateLogin_WithValidData_ReturnsNull()
    {
        // Arrange
        var email = "test@example.com";
        var password = "password123";

        // Act
        var result = AuthValidator.ValidateLogin(email, password);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("user123")]
    [InlineData("test_user")]
    [InlineData("User_Name_123")]
    [InlineData("abc")]  // минимальная длина
    public void ValidateUsername_WithValidUsernames_ReturnsNull(string username)
    {
        // Act
        var result = AuthValidator.ValidateUsername(username);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user@domain.org")]
    [InlineData("name.surname@company.co.uk")]
    [InlineData("a@b.c")]
    public void ValidateEmail_WithValidEmails_ReturnsNull(string email)
    {
        // Act
        var result = AuthValidator.ValidateEmail(email);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Empty Fields Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateUsername_WithEmptyValue_ReturnsError(string? username)
    {
        // Act
        var result = AuthValidator.ValidateUsername(username);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Username is required", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateEmail_WithEmptyValue_ReturnsError(string? email)
    {
        // Act
        var result = AuthValidator.ValidateEmail(email);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Email is required", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidatePassword_WithEmptyValue_ReturnsError(string? password)
    {
        // Act
        var result = AuthValidator.ValidatePassword(password);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Password is required", result);
    }

    [Fact]
    public void ValidateRegistration_WithAllEmptyFields_ReturnsUsernameError()
    {
        // Arrange — пустые поля (первая ошибка должна быть про username)
        var result = AuthValidator.ValidateRegistration(null, null, null, null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Username is required", result);
    }

    #endregion

    #region Short Username Tests

    [Theory]
    [InlineData("a")]
    [InlineData("ab")]
    public void ValidateUsername_WithShortUsername_ReturnsError(string username)
    {
        // Act
        var result = AuthValidator.ValidateUsername(username);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("at least", result);
        Assert.Contains(AuthValidator.MinUsernameLength.ToString(), result);
    }

    #endregion

    #region Invalid Email Format Tests

    [Theory]
    [InlineData("notanemail")]
    [InlineData("missing@domain")]
    [InlineData("@nodomain.com")]
    [InlineData("spaces in@email.com")]
    [InlineData("no@dots")]
    public void ValidateEmail_WithInvalidFormat_ReturnsError(string email)
    {
        // Act
        var result = AuthValidator.ValidateEmail(email);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Invalid email format", result);
    }

    #endregion

    #region Password Mismatch Tests

    [Fact]
    public void ValidatePasswordConfirmation_WithDifferentPasswords_ReturnsError()
    {
        // Arrange
        var password = "password123";
        var confirmPassword = "differentpassword";

        // Act
        var result = AuthValidator.ValidatePasswordConfirmation(password, confirmPassword);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Passwords do not match", result);
    }

    [Fact]
    public void ValidatePasswordConfirmation_WithMatchingPasswords_ReturnsNull()
    {
        // Arrange
        var password = "password123";
        var confirmPassword = "password123";

        // Act
        var result = AuthValidator.ValidatePasswordConfirmation(password, confirmPassword);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ValidateRegistration_WithMismatchedPasswords_ReturnsError()
    {
        // Arrange
        var username = "validuser";
        var email = "test@example.com";
        var password = "password123";
        var confirmPassword = "wrongpassword";

        // Act
        var result = AuthValidator.ValidateRegistration(username, email, password, confirmPassword);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Passwords do not match", result);
    }

    #endregion

    #region Invalid Username Characters Tests

    [Theory]
    [InlineData("user name")]    // пробел
    [InlineData("user@name")]    // @
    [InlineData("user-name")]    // дефис
    [InlineData("user.name")]    // точка
    [InlineData("имя")]          // кириллица
    [InlineData("user!name")]    // восклицательный знак
    public void ValidateUsername_WithInvalidCharacters_ReturnsError(string username)
    {
        // Act
        var result = AuthValidator.ValidateUsername(username);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Username can only contain letters, numbers and underscores", result);
    }

    #endregion

    #region Short Password Tests

    [Theory]
    [InlineData("12345")]   // 5 символов
    [InlineData("abc")]     // 3 символа
    [InlineData("a")]       // 1 символ
    public void ValidatePassword_WithShortPassword_ReturnsError(string password)
    {
        // Act
        var result = AuthValidator.ValidatePassword(password);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("at least", result);
        Assert.Contains(AuthValidator.MinPasswordLength.ToString(), result);
    }

    [Fact]
    public void ValidatePassword_WithMinimumLength_ReturnsNull()
    {
        // Arrange — ровно минимальная длина
        var password = new string('a', AuthValidator.MinPasswordLength);

        // Act
        var result = AuthValidator.ValidatePassword(password);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Long Username Tests

    [Fact]
    public void ValidateUsername_WithTooLongUsername_ReturnsError()
    {
        // Arrange — превышаем максимальную длину
        var username = new string('a', AuthValidator.MaxUsernameLength + 1);

        // Act
        var result = AuthValidator.ValidateUsername(username);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("at most", result);
        Assert.Contains(AuthValidator.MaxUsernameLength.ToString(), result);
    }

    [Fact]
    public void ValidateUsername_WithMaximumLength_ReturnsNull()
    {
        // Arrange — ровно максимальная длина
        var username = new string('a', AuthValidator.MaxUsernameLength);

        // Act
        var result = AuthValidator.ValidateUsername(username);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region Login Validation Tests

    [Fact]
    public void ValidateLogin_WithEmptyEmail_ReturnsError()
    {
        // Act
        var result = AuthValidator.ValidateLogin("", "password123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Email is required", result);
    }

    [Fact]
    public void ValidateLogin_WithInvalidEmail_ReturnsError()
    {
        // Act
        var result = AuthValidator.ValidateLogin("notanemail", "password123");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Invalid email format", result);
    }

    [Fact]
    public void ValidateLogin_WithEmptyPassword_ReturnsError()
    {
        // Act
        var result = AuthValidator.ValidateLogin("test@example.com", "");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Password is required", result);
    }

    #endregion
}
