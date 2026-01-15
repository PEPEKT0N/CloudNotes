using CloudNotes.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CloudNotes.Api.Services;

/// <summary>
/// Кастомный UserManager, который НЕ требует уникальности UserName.
/// Переопределяет CreateAsync чтобы пропустить стандартную проверку дублирования имени пользователя.
/// </summary>
public class CustomUserManager : UserManager<User>
{
    public CustomUserManager(
        IUserStore<User> store,
        IOptions<IdentityOptions> optionsAccessor,
        IPasswordHasher<User> passwordHasher,
        IEnumerable<IUserValidator<User>> userValidators,
        IEnumerable<IPasswordValidator<User>> passwordValidators,
        ILookupNormalizer keyNormalizer,
        IdentityErrorDescriber errors,
        IServiceProvider services,
        ILogger<UserManager<User>> logger)
        : base(store, optionsAccessor, passwordHasher, userValidators, passwordValidators, keyNormalizer, errors, services, logger)
    {
    }

    /// <summary>
    /// Создание пользователя с кастомной валидацией.
    /// НЕ проверяет уникальность UserName, только Email.
    /// </summary>
    public override async Task<IdentityResult> CreateAsync(User user, string password)
    {
        ThrowIfDisposed();
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentNullException(nameof(password));
        }

        // Кастомная валидация (без проверки уникальности UserName)
        var validationResult = await ValidateUserInternalAsync(user);
        if (!validationResult.Succeeded)
        {
            return validationResult;
        }

        // Валидация пароля
        foreach (var validator in PasswordValidators)
        {
            var passwordResult = await validator.ValidateAsync(this, user, password);
            if (!passwordResult.Succeeded)
            {
                return passwordResult;
            }
        }

        // Нормализация
        await UpdateNormalizedUserNameAsync(user);
        await UpdateNormalizedEmailAsync(user);

        // Хеширование пароля
        user.PasswordHash = PasswordHasher.HashPassword(user, password);

        // Установка SecurityStamp
        await UpdateSecurityStampAsync(user);

        // Создание в store
        return await Store.CreateAsync(user, CancellationToken);
    }

    /// <summary>
    /// Создание пользователя без пароля с кастомной валидацией.
    /// </summary>
    public override async Task<IdentityResult> CreateAsync(User user)
    {
        ThrowIfDisposed();
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        // Кастомная валидация
        var validationResult = await ValidateUserInternalAsync(user);
        if (!validationResult.Succeeded)
        {
            return validationResult;
        }

        // Нормализация
        await UpdateNormalizedUserNameAsync(user);
        await UpdateNormalizedEmailAsync(user);

        // Установка SecurityStamp
        await UpdateSecurityStampAsync(user);

        // Создание в store
        return await Store.CreateAsync(user, CancellationToken);
    }

    /// <summary>
    /// Внутренняя валидация пользователя.
    /// НЕ проверяет уникальность UserName, только Email.
    /// </summary>
    private async Task<IdentityResult> ValidateUserInternalAsync(User user)
    {
        var errors = new List<IdentityError>();

        // Проверяем что UserName не пустой
        if (string.IsNullOrWhiteSpace(user.UserName))
        {
            errors.Add(new IdentityError
            {
                Code = "InvalidUserName",
                Description = "Имя пользователя не может быть пустым."
            });
        }

        // Проверяем что Email не пустой
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            errors.Add(new IdentityError
            {
                Code = "InvalidEmail",
                Description = "Email не может быть пустым."
            });
        }

        // Проверяем уникальность Email
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var existingUser = await FindByEmailAsync(user.Email);
            if (existingUser != null && !string.Equals(existingUser.Id, user.Id))
            {
                errors.Add(new IdentityError
                {
                    Code = "DuplicateEmail",
                    Description = $"Email '{user.Email}' уже используется."
                });
            }
        }

        // НЕ проверяем уникальность UserName!

        return errors.Count > 0
            ? IdentityResult.Failed(errors.ToArray())
            : IdentityResult.Success;
    }
}
