using System;

namespace CloudNotes.Desktop.Api.DTOs;

/// <summary>
/// DTO для создания новой папки.
/// </summary>
public class CreateFolderDto
{
    /// <summary>
    /// Название папки.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Идентификатор родительской папки (null для корневых папок).
    /// </summary>
    public Guid? ParentFolderId { get; set; }
}
