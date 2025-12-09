using CloudNotes.Api.Data;
using CloudNotes.Api.DTOs.Notes;
using CloudNotes.Api.Extensions;
using CloudNotes.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CloudNotes.Api.Controllers;

/// <summary>
/// Контроллер для работы с заметками.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotesController : ControllerBase
{
    private readonly ApiDbContext _context;
    private readonly ILogger<NotesController> _logger;

    public NotesController(ApiDbContext context, ILogger<NotesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Получить все заметки текущего пользователя.
    /// </summary>
    /// <returns>Список заметок.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<NoteDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var userId = HttpContext.GetRequiredUserId();

        var notes = await _context.Notes
            .Where(n => n.UserId == userId && !n.IsDeleted)
            .OrderByDescending(n => n.UpdatedAt)
            .Select(n => MapToDto(n))
            .ToListAsync();

        return Ok(notes);
    }

    /// <summary>
    /// Получить заметку по ID.
    /// </summary>
    /// <param name="id">ID заметки.</param>
    /// <returns>Заметка.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = HttpContext.GetRequiredUserId();

        var note = await _context.Notes
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId && !n.IsDeleted);

        if (note == null)
        {
            return NotFound(new { error = "Заметка не найдена" });
        }

        return Ok(MapToDto(note));
    }

    /// <summary>
    /// Получить заметки по тегу.
    /// </summary>
    /// <param name="tag">Название тега.</param>
    /// <returns>Список заметок с указанным тегом.</returns>
    [HttpGet("by-tag/{tag}")]
    [ProducesResponseType(typeof(IEnumerable<NoteDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByTag(string tag)
    {
        var userId = HttpContext.GetRequiredUserId();

        var notes = await _context.Notes
            .Where(n => n.UserId == userId && !n.IsDeleted &&
                        n.NoteTags.Any(nt => EF.Functions.ILike(nt.Tag.Name, tag)))
            .OrderByDescending(n => n.UpdatedAt)
            .Select(n => MapToDto(n))
            .ToListAsync();

        return Ok(notes);
    }

    /// <summary>
    /// Поиск заметок по заголовку.
    /// </summary>
    /// <param name="title">Часть заголовка для поиска.</param>
    /// <returns>Список заметок.</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<NoteDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string title)
    {
        var userId = HttpContext.GetRequiredUserId();

        var notes = await _context.Notes
            .Where(n => n.UserId == userId && !n.IsDeleted &&
                        EF.Functions.ILike(n.Title, $"%{title}%"))
            .OrderByDescending(n => n.UpdatedAt)
            .Select(n => MapToDto(n))
            .ToListAsync();

        return Ok(notes);
    }

    /// <summary>
    /// Создать новую заметку.
    /// </summary>
    /// <param name="dto">Данные заметки.</param>
    /// <returns>Созданная заметка.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(NoteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateNoteDto dto)
    {
        var userId = HttpContext.GetRequiredUserId();

        var note = new Note
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = dto.Title,
            Content = dto.Content,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        };

        _context.Notes.Add(note);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Пользователь {UserId} создал заметку {NoteId}", userId, note.Id);

        return CreatedAtAction(nameof(GetById), new { id = note.Id }, MapToDto(note));
    }

    /// <summary>
    /// Обновить заметку.
    /// </summary>
    /// <param name="id">ID заметки.</param>
    /// <param name="dto">Новые данные заметки.</param>
    /// <returns>Обновлённая заметка.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(NoteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNoteDto dto)
    {
        var userId = HttpContext.GetRequiredUserId();

        var note = await _context.Notes
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId && !n.IsDeleted);

        if (note == null)
        {
            return NotFound(new { error = "Заметка не найдена" });
        }

        // Конфликт-резолвер (Last Write Wins)
        if (dto.ClientUpdatedAt.HasValue && dto.ClientUpdatedAt < note.UpdatedAt)
        {
            return Conflict(new
            {
                error = "conflict",
                message = "Версия на сервере новее",
                serverNote = MapToDto(note)
            });
        }

        note.Title = dto.Title;
        note.Content = dto.Content;
        note.UpdatedAt = DateTime.UtcNow;
        note.SyncedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Пользователь {UserId} обновил заметку {NoteId}", userId, note.Id);

        return Ok(MapToDto(note));
    }

    /// <summary>
    /// Удалить заметку.
    /// </summary>
    /// <param name="id">ID заметки.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = HttpContext.GetRequiredUserId();

        var note = await _context.Notes
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId && !n.IsDeleted);

        if (note == null)
        {
            return NotFound(new { error = "Заметка не найдена" });
        }

        // Жёсткое удаление (согласно .cursorrules)
        _context.Notes.Remove(note);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Пользователь {UserId} удалил заметку {NoteId}", userId, note.Id);

        return NoContent();
    }

    /// <summary>
    /// Маппинг Note -> NoteDto.
    /// </summary>
    private static NoteDto MapToDto(Note note)
    {
        return new NoteDto
        {
            Id = note.Id,
            Title = note.Title,
            Content = note.Content,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt,
            SyncedAt = note.SyncedAt
        };
    }
}

