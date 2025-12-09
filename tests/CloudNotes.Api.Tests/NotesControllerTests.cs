using System.Security.Claims;
using CloudNotes.Api.Controllers;
using CloudNotes.Api.Data;
using CloudNotes.Api.DTOs.Notes;
using CloudNotes.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CloudNotes.Api.Tests;

/// <summary>
/// Тесты для NotesController.
/// </summary>
public class NotesControllerTests : IDisposable
{
    private readonly ApiDbContext _context;
    private readonly NotesController _controller;
    private readonly string _userId = "test-user-id";
    private readonly string _otherUserId = "other-user-id";

    public NotesControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApiDbContext(options);

        var logger = new Mock<ILogger<NotesController>>();
        _controller = new NotesController(_context, logger.Object);

        // Setup HttpContext with authenticated user
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId)
        }, "test"));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetAll Tests

    [Fact]
    public async Task GetAll_ReturnsOnlyUserNotes()
    {
        // Arrange
        await SeedNotes();

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var notes = Assert.IsAssignableFrom<IEnumerable<NoteDto>>(okResult.Value);
        Assert.Equal(2, notes.Count()); // Only user's notes, not other user's
    }

    [Fact]
    public async Task GetAll_ExcludesDeletedNotes()
    {
        // Arrange
        var deletedNote = new Note
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Title = "Deleted Note",
            IsDeleted = true
        };
        _context.Notes.Add(deletedNote);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetAll();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var notes = Assert.IsAssignableFrom<IEnumerable<NoteDto>>(okResult.Value);
        Assert.DoesNotContain(notes, n => n.Title == "Deleted Note");
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetById_WithValidId_ReturnsNote()
    {
        // Arrange
        var note = await CreateNote("Test Note");

        // Act
        var result = await _controller.GetById(note.Id);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<NoteDto>(okResult.Value);
        Assert.Equal(note.Id, dto.Id);
        Assert.Equal("Test Note", dto.Title);
    }

    [Fact]
    public async Task GetById_WithOtherUserNote_ReturnsNotFound()
    {
        // Arrange
        var otherUserNote = new Note
        {
            Id = Guid.NewGuid(),
            UserId = _otherUserId,
            Title = "Other User Note"
        };
        _context.Notes.Add(otherUserNote);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetById(otherUserNote.Id);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetById(Guid.NewGuid());

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task Create_WithValidData_ReturnsCreatedNote()
    {
        // Arrange
        var dto = new CreateNoteDto
        {
            Title = "New Note",
            Content = "Content"
        };

        // Act
        var result = await _controller.Create(dto);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        var noteDto = Assert.IsType<NoteDto>(createdResult.Value);
        Assert.Equal("New Note", noteDto.Title);
        Assert.Equal("Content", noteDto.Content);
    }

    [Fact]
    public async Task Create_SavesNoteWithCorrectUserId()
    {
        // Arrange
        var dto = new CreateNoteDto { Title = "New Note" };

        // Act
        await _controller.Create(dto);

        // Assert
        var savedNote = await _context.Notes.FirstOrDefaultAsync(n => n.Title == "New Note");
        Assert.NotNull(savedNote);
        Assert.Equal(_userId, savedNote.UserId);
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task Update_WithValidData_ReturnsUpdatedNote()
    {
        // Arrange
        var note = await CreateNote("Original Title");
        var dto = new UpdateNoteDto
        {
            Title = "Updated Title",
            Content = "Updated Content"
        };

        // Act
        var result = await _controller.Update(note.Id, dto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var noteDto = Assert.IsType<NoteDto>(okResult.Value);
        Assert.Equal("Updated Title", noteDto.Title);
        Assert.Equal("Updated Content", noteDto.Content);
    }

    [Fact]
    public async Task Update_WithOtherUserNote_ReturnsNotFound()
    {
        // Arrange
        var otherUserNote = new Note
        {
            Id = Guid.NewGuid(),
            UserId = _otherUserId,
            Title = "Other User Note"
        };
        _context.Notes.Add(otherUserNote);
        await _context.SaveChangesAsync();

        var dto = new UpdateNoteDto { Title = "Hacked!" };

        // Act
        var result = await _controller.Update(otherUserNote.Id, dto);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Update_WithOldClientVersion_ReturnsConflict()
    {
        // Arrange
        var note = await CreateNote("Original");
        note.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var dto = new UpdateNoteDto
        {
            Title = "Updated",
            ClientUpdatedAt = DateTime.UtcNow.AddHours(-1) // Older than server
        };

        // Act
        var result = await _controller.Update(note.Id, dto);

        // Assert
        Assert.IsType<ConflictObjectResult>(result);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Delete_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var note = await CreateNote("To Delete");

        // Act
        var result = await _controller.Delete(note.Id);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_RemovesNoteFromDatabase()
    {
        // Arrange
        var note = await CreateNote("To Delete");
        var noteId = note.Id;

        // Act
        await _controller.Delete(noteId);

        // Assert
        var deletedNote = await _context.Notes.FindAsync(noteId);
        Assert.Null(deletedNote);
    }

    [Fact]
    public async Task Delete_WithOtherUserNote_ReturnsNotFound()
    {
        // Arrange
        var otherUserNote = new Note
        {
            Id = Guid.NewGuid(),
            UserId = _otherUserId,
            Title = "Other User Note"
        };
        _context.Notes.Add(otherUserNote);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.Delete(otherUserNote.Id);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
        // Verify note still exists
        var stillExists = await _context.Notes.FindAsync(otherUserNote.Id);
        Assert.NotNull(stillExists);
    }

    #endregion

    #region Helper Methods

    private async Task<Note> CreateNote(string title)
    {
        var note = new Note
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Title = title,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Notes.Add(note);
        await _context.SaveChangesAsync();
        return note;
    }

    private async Task SeedNotes()
    {
        var notes = new[]
        {
            new Note { Id = Guid.NewGuid(), UserId = _userId, Title = "Note 1" },
            new Note { Id = Guid.NewGuid(), UserId = _userId, Title = "Note 2" },
            new Note { Id = Guid.NewGuid(), UserId = _otherUserId, Title = "Other User Note" }
        };
        _context.Notes.AddRange(notes);
        await _context.SaveChangesAsync();
    }

    #endregion
}

