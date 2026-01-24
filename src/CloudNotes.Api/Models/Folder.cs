using System.ComponentModel.DataAnnotations;

namespace CloudNotes.Api.Models;

/// <summary>
/// Папка/каталог для организации заметок пользователя.
/// </summary>
public class Folder
{
    /// <summary>
    /// Уникальный идентификатор папки.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Идентификатор владельца папки.
    /// </summary>
    public string UserId { get; set; } = null!;

    /// <summary>
    /// Владелец папки.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Название папки.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор родительской папки (null для корневых папок).
    /// </summary>
    public Guid? ParentFolderId { get; set; }

    /// <summary>
    /// Родительская папка.
    /// </summary>
    public Folder? ParentFolder { get; set; }

    /// <summary>
    /// Дочерние папки.
    /// </summary>
    public ICollection<Folder> ChildFolders { get; set; } = new List<Folder>();

    /// <summary>
    /// Заметки в этой папке.
    /// </summary>
    public ICollection<Note> Notes { get; set; } = new List<Note>();

    /// <summary>
    /// Дата создания папки.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Дата последнего обновления папки.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
