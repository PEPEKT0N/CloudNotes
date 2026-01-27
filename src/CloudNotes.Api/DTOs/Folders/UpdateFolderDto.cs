using System.ComponentModel.DataAnnotations;

namespace CloudNotes.Api.DTOs.Folders;

/// <summary>
/// DTO для обновления папки.
/// </summary>
public class UpdateFolderDto
{
    /// <summary>
    /// Новое название папки.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Новый идентификатор родительской папки (null для корневых папок).
    /// </summary>
    public Guid? ParentFolderId { get; set; }
}
