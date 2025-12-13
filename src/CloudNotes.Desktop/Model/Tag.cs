using System;
using System.ComponentModel.DataAnnotations;

namespace CloudNotes.Desktop.Model;

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
}
