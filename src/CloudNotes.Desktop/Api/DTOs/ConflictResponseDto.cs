using System;

namespace CloudNotes.Desktop.Api.DTOs;

// DTO для ответа 409 Conflict от сервера
public class ConflictResponseDto
{
    public string Error { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public NoteDto ServerNote { get; set; } = null!;
}

