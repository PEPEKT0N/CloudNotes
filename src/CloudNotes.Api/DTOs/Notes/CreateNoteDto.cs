using System.ComponentModel.DataAnnotations;

namespace CloudNotes.Api.DTOs.Notes;

/// <summary>
/// DTO для создания заметки.
/// </summary>
public class CreateNoteDto
{
    /// <summary>
    /// Заголовок заметки.
    /// </summary>
    [Required(ErrorMessage = "Заголовок обязателен")]
    [MaxLength(255, ErrorMessage = "Заголовок не может превышать 255 символов")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Содержимое заметки (Markdown).
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Идентификатор папки, в которой находится заметка (null если заметка не в папке).
    /// </summary>
    public Guid? FolderId { get; set; }

    /// <summary>
    /// Названия тегов заметки.
    /// </summary>
    public IList<string> Tags { get; set; } = new List<string>();
}

