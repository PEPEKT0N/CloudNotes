using Microsoft.AspNetCore.Identity;

namespace CloudNotes.Api.Models;

/// <summary>
/// Пользователь системы. Расширяет стандартный IdentityUser.
/// </summary>
public class User : IdentityUser
{
    /// <summary>
    /// Дата регистрации пользователя.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Коллекция заметок пользователя.
    /// </summary>
    public ICollection<Note> Notes { get; set; } = new List<Note>();

    /// <summary>
    /// Коллекция папок пользователя.
    /// </summary>
    public ICollection<Folder> Folders { get; set; } = new List<Folder>();

    /// <summary>
    /// Коллекция refresh-токенов пользователя.
    /// </summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

