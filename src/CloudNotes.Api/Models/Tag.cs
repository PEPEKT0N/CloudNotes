using System.ComponentModel.DataAnnotations;

namespace CloudNotes.Api.Models;

/// <summary>
/// Тег для категоризации заметок.
/// </summary>
public class Tag
{
    /// <summary>
    /// Уникальный идентификатор тега.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Название тега.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Заметки с этим тегом (Many-to-Many через NoteTag).
    /// </summary>
    public ICollection<NoteTag> NoteTags { get; set; } = new List<NoteTag>();
}

