using System;

namespace CloudNotes.Desktop.Model;

/// <summary>
/// Модель для отслеживания удалённых заметок, которые нужно синхронизировать с сервером.
/// После удаления заметки её ServerId сохраняется здесь для последующей синхронизации.
/// </summary>
public class DeletedNote
{
    public Guid Id { get; set; }

    /// <summary>
    /// ServerId удалённой заметки (для удаления на сервере).
    /// </summary>
    public Guid ServerId { get; set; }

    /// <summary>
    /// Email пользователя-владельца заметки.
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// Дата удаления.
    /// </summary>
    public DateTime DeletedAt { get; set; }
}
