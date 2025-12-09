namespace CloudNotes.Desktop.Api.DTOs;

/// <summary>
/// DTO для создания заметки.
/// </summary>
public class CreateNoteDto
{
    /// <summary>
    /// Заголовок заметки.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Содержимое заметки (Markdown).
    /// </summary>
    public string? Content { get; set; }
}

