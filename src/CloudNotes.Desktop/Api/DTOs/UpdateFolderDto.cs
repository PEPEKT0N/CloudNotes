using System;

namespace CloudNotes.Desktop.Api.DTOs;

/// <summary>
/// DTO для обновления папки.
/// </summary>
public class UpdateFolderDto
{
    /// <summary>
    /// Новое название папки.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Новый идентификатор родительской папки (null для корневых папок).
    /// </summary>
    public Guid? ParentFolderId { get; set; }
}
