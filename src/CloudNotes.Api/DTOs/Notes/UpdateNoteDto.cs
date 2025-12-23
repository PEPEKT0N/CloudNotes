using System.ComponentModel.DataAnnotations;

namespace CloudNotes.Api.DTOs.Notes;

/// <summary>
/// DTO для обновления заметки.
/// </summary>
public class UpdateNoteDto
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
    /// Время последнего обновления на клиенте (для конфликт-резолвера).
    /// </summary>
    public DateTime? ClientUpdatedAt { get; set; }

    /// <summary>
    /// Названия тегов заметки.
    /// </summary>
    public IList<string> Tags { get; set; } = new List<string>();
}

