using CloudNotes.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace CloudNotes.Api.Services;

/// <summary>
/// Кастомный валидатор пользователя, который НЕ требует уникальности UserName.
/// Уникальным должен быть только Email.
/// </summary>
public class CustomUserValidator : IUserValidator<User>
{
    /// <summary>
    /// Валидация пользователя. Проверяет только email (уникальность email проверяется Identity автоматически).
    /// UserName может быть неуникальным.
    /// </summary>
    public async Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user)
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

        // Проверяем уникальность Email (если email изменился или новый пользователь)
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var existingUser = await manager.FindByEmailAsync(user.Email);
            if (existingUser != null && !string.Equals(existingUser.Id, user.Id))
            {
                errors.Add(new IdentityError
                {
                    Code = "DuplicateEmail",
                    Description = $"Email '{user.Email}' уже используется."
                });
            }
        }

        // НЕ проверяем уникальность UserName — это и есть цель этого валидатора

        return errors.Count > 0
            ? IdentityResult.Failed(errors.ToArray())
            : IdentityResult.Success;
    }
}
