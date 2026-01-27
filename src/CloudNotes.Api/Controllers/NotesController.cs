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
    /// <param name="folderId">Фильтр по папке (null для всех заметок).</param>
    /// <returns>Список заметок.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<NoteDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? folderId = null)
    {
        var userId = HttpContext.GetRequiredUserId();

        var query = _context.Notes
            .Where(n => n.UserId == userId && !n.IsDeleted);

        // Фильтрация по папке (если указана)
        if (folderId.HasValue)
        {
            query = query.Where(n => n.FolderId == folderId.Value);
        }
        // Если folderId не указан, показываем все заметки (и с папками, и без)

        var notes = await query
            .Include(n => n.NoteTags)
                .ThenInclude(nt => nt.Tag)
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync();

        var noteDtos = notes.Select(n => MapToDto(n)).ToList();

        return Ok(noteDtos);
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
            .Include(n => n.NoteTags)
                .ThenInclude(nt => nt.Tag)
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
            .Include(n => n.NoteTags)
                .ThenInclude(nt => nt.Tag)
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync();

        var noteDtos = notes.Select(n => MapToDto(n)).ToList();

        return Ok(noteDtos);
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
            .Include(n => n.NoteTags)
                .ThenInclude(nt => nt.Tag)
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync();

        var noteDtos = notes.Select(n => MapToDto(n)).ToList();

        return Ok(noteDtos);
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

        // Проверяем, что папка существует и принадлежит пользователю (если указана)
        if (dto.FolderId.HasValue)
        {
            var folder = await _context.Folders
                .FirstOrDefaultAsync(f => f.Id == dto.FolderId.Value && f.UserId == userId);

            if (folder == null)
            {
                return BadRequest(new { error = "Папка не найдена" });
            }
        }

        var note = new Note
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = dto.Title,
            Content = dto.Content,
            FolderId = dto.FolderId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SyncedAt = DateTime.UtcNow
        };

        _context.Notes.Add(note);
        await _context.SaveChangesAsync();

        // Обработка тегов
        await SyncNoteTagsAsync(note.Id, dto.Tags);

        // Перезагружаем заметку с тегами для возврата
        await _context.Entry(note)
            .Collection(n => n.NoteTags)
            .Query()
            .Include(nt => nt.Tag)
            .LoadAsync();

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

        // Проверяем, что папка существует и принадлежит пользователю (если указана)
        if (dto.FolderId.HasValue)
        {
            var folder = await _context.Folders
                .FirstOrDefaultAsync(f => f.Id == dto.FolderId.Value && f.UserId == userId);

            if (folder == null)
            {
                return BadRequest(new { error = "Папка не найдена" });
            }
        }

        note.Title = dto.Title;
        note.Content = dto.Content;
        note.FolderId = dto.FolderId;
        note.UpdatedAt = DateTime.UtcNow;
        note.SyncedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Обработка тегов
        await SyncNoteTagsAsync(note.Id, dto.Tags);

        // Перезагружаем заметку с тегами для возврата
        await _context.Entry(note)
            .Collection(n => n.NoteTags)
            .Query()
            .Include(nt => nt.Tag)
            .LoadAsync();

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
            SyncedAt = note.SyncedAt,
            FolderId = note.FolderId,
            Tags = note.NoteTags?.Select(nt => nt.Tag.Name).ToList() ?? new List<string>()
        };
    }

    /// <summary>
    /// Синхронизирует теги заметки с переданным списком названий тегов.
    /// </summary>
    /// <param name="noteId">ID заметки.</param>
    /// <param name="tagNames">Список названий тегов.</param>
    private async Task SyncNoteTagsAsync(Guid noteId, IList<string> tagNames)
    {
        // Получаем текущие связи заметки с тегами
        var existingNoteTags = await _context.NoteTags
            .Where(nt => nt.NoteId == noteId)
            .Include(nt => nt.Tag)
            .ToListAsync();

        var existingTagNames = existingNoteTags.Select(nt => nt.Tag.Name.ToLowerInvariant()).ToHashSet();

        // Создаем словарь для сохранения оригинальных имен (с правильным регистром)
        var originalNamesMap = new Dictionary<string, string>();
        var requestedTagNames = new HashSet<string>();

        if (tagNames != null)
        {
            foreach (var tagName in tagNames)
            {
                if (string.IsNullOrWhiteSpace(tagName))
                {
                    continue;
                }

                var trimmedName = tagName.Trim();
                var lowerName = trimmedName.ToLowerInvariant();

                if (!originalNamesMap.ContainsKey(lowerName))
                {
                    originalNamesMap[lowerName] = trimmedName;
                    requestedTagNames.Add(lowerName);
                }
            }
        }

        // Удаляем теги, которых больше нет в запросе
        var tagsToRemove = existingNoteTags
            .Where(nt => !requestedTagNames.Contains(nt.Tag.Name.ToLowerInvariant()))
            .ToList();

        foreach (var noteTag in tagsToRemove)
        {
            _context.NoteTags.Remove(noteTag);
        }

        // Добавляем новые теги
        var tagsToAdd = requestedTagNames.Where(tn => !existingTagNames.Contains(tn)).ToList();
        foreach (var tagNameLower in tagsToAdd)
        {
            // Ищем существующий тег по имени (case-insensitive)
            var tag = await _context.Tags
                .FirstOrDefaultAsync(t => t.Name.ToLowerInvariant() == tagNameLower);

            // Если тег не существует, создаем новый
            if (tag == null)
            {
                var originalName = originalNamesMap[tagNameLower];
                tag = new Tag
                {
                    Id = Guid.NewGuid(),
                    Name = originalName
                };
                _context.Tags.Add(tag);
                await _context.SaveChangesAsync();
            }

            // Создаем связь между заметкой и тегом
            var noteTag = new NoteTag
            {
                NoteId = noteId,
                TagId = tag.Id
            };
            _context.NoteTags.Add(noteTag);
        }

        await _context.SaveChangesAsync();
    }
}

