using System.ComponentModel.DataAnnotations;

namespace CloudNotes.Api.DTOs.Folders;

/// <summary>
/// DTO для создания новой папки.
/// </summary>
public class CreateFolderDto
{
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
}
