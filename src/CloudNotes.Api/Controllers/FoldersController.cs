using CloudNotes.Api.Data;
using CloudNotes.Api.DTOs.Folders;
using CloudNotes.Api.Extensions;
using CloudNotes.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CloudNotes.Api.Controllers;

/// <summary>
/// Контроллер для работы с папками.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FoldersController : ControllerBase
{
    private readonly ApiDbContext _context;
    private readonly ILogger<FoldersController> _logger;

    public FoldersController(ApiDbContext context, ILogger<FoldersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Получить все папки текущего пользователя.
    /// </summary>
    /// <returns>Список папок.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<FolderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var userId = HttpContext.GetRequiredUserId();

        var folders = await _context.Folders
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.Name)
            .ToListAsync();

        var folderDtos = folders.Select(f => MapToDto(f)).ToList();

        return Ok(folderDtos);
    }

    /// <summary>
    /// Получить папку по ID.
    /// </summary>
    /// <param name="id">ID папки.</param>
    /// <returns>Папка.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FolderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId = HttpContext.GetRequiredUserId();

        var folder = await _context.Folders
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

        if (folder == null)
        {
            return NotFound(new { error = "Папка не найдена" });
        }

        return Ok(MapToDto(folder));
    }

    /// <summary>
    /// Создать новую папку.
    /// </summary>
    /// <param name="dto">Данные папки.</param>
    /// <returns>Созданная папка.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(FolderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateFolderDto dto)
    {
        var userId = HttpContext.GetRequiredUserId();

        // Проверяем, что родительская папка существует и принадлежит пользователю (если указана)
        if (dto.ParentFolderId.HasValue)
        {
            var parentFolder = await _context.Folders
                .FirstOrDefaultAsync(f => f.Id == dto.ParentFolderId.Value && f.UserId == userId);

            if (parentFolder == null)
            {
                return NotFound(new { error = "Родительская папка не найдена" });
            }
        }

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = dto.Name,
            ParentFolderId = dto.ParentFolderId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Folders.Add(folder);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Пользователь {UserId} создал папку {FolderId}", userId, folder.Id);

        return CreatedAtAction(nameof(GetById), new { id = folder.Id }, MapToDto(folder));
    }

    /// <summary>
    /// Обновить папку.
    /// </summary>
    /// <param name="id">ID папки.</param>
    /// <param name="dto">Новые данные папки.</param>
    /// <returns>Обновлённая папка.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(FolderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFolderDto dto)
    {
        var userId = HttpContext.GetRequiredUserId();

        var folder = await _context.Folders
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

        if (folder == null)
        {
            return NotFound(new { error = "Папка не найдена" });
        }

        // Проверяем, что новая родительская папка существует и принадлежит пользователю (если указана)
        if (dto.ParentFolderId.HasValue)
        {
            // Нельзя сделать папку родителем самой себя
            if (dto.ParentFolderId.Value == id)
            {
                return BadRequest(new { error = "Папка не может быть родителем самой себя" });
            }

            var parentFolder = await _context.Folders
                .FirstOrDefaultAsync(f => f.Id == dto.ParentFolderId.Value && f.UserId == userId);

            if (parentFolder == null)
            {
                return NotFound(new { error = "Родительская папка не найдена" });
            }

            // Проверяем, что не создается циклическая зависимость
            if (await WouldCreateCycleAsync(id, dto.ParentFolderId.Value, userId))
            {
                return BadRequest(new { error = "Невозможно создать циклическую зависимость" });
            }
        }

        folder.Name = dto.Name;
        folder.ParentFolderId = dto.ParentFolderId;
        folder.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Пользователь {UserId} обновил папку {FolderId}", userId, folder.Id);

        return Ok(MapToDto(folder));
    }

    /// <summary>
    /// Удалить папку.
    /// </summary>
    /// <param name="id">ID папки.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = HttpContext.GetRequiredUserId();

        var folder = await _context.Folders
            .Include(f => f.ChildFolders)
            .FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);

        if (folder == null)
        {
            return NotFound(new { error = "Папка не найдена" });
        }

        // Проверяем, что в папке нет дочерних папок
        if (folder.ChildFolders.Any())
        {
            return BadRequest(new { error = "Невозможно удалить папку, содержащую дочерние папки" });
        }

        // При удалении папки заметки остаются без папки (FolderId становится null)
        // Это обрабатывается через OnDelete(DeleteBehavior.SetNull) в конфигурации

        _context.Folders.Remove(folder);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Пользователь {UserId} удалил папку {FolderId}", userId, folder.Id);

        return NoContent();
    }

    /// <summary>
    /// Маппинг Folder -> FolderDto.
    /// </summary>
    private static FolderDto MapToDto(Folder folder)
    {
        return new FolderDto
        {
            Id = folder.Id,
            Name = folder.Name,
            ParentFolderId = folder.ParentFolderId,
            CreatedAt = folder.CreatedAt,
            UpdatedAt = folder.UpdatedAt
        };
    }

    /// <summary>
    /// Проверяет, создаст ли установка нового родителя циклическую зависимость.
    /// </summary>
    private async Task<bool> WouldCreateCycleAsync(Guid folderId, Guid newParentId, string userId)
    {
        // Проверяем, не является ли текущая папка предком новой родительской папки
        Guid? currentParentId = newParentId;
        var visited = new HashSet<Guid> { folderId };

        while (currentParentId.HasValue)
        {
            if (visited.Contains(currentParentId.Value))
            {
                return true; // Найден цикл
            }

            visited.Add(currentParentId.Value);

            var parent = await _context.Folders
                .FirstOrDefaultAsync(f => f.Id == currentParentId.Value && f.UserId == userId);

            if (parent == null || parent.ParentFolderId == null)
            {
                break;
            }

            currentParentId = parent.ParentFolderId;
        }

        return false;
    }
}
