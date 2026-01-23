namespace CloudNotes.Api.DTOs.Folders;

/// <summary>
/// DTO для представления папки.
/// </summary>
public class FolderDto
{
    /// <summary>
    /// Уникальный идентификатор папки.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Название папки.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор родительской папки (null для корневых папок).
    /// </summary>
    public Guid? ParentFolderId { get; set; }

    /// <summary>
    /// Дата создания папки.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Дата последнего обновления папки.
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
