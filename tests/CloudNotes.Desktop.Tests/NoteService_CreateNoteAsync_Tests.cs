using System;
using System.Threading.Tasks;
using CloudNotes.Desktop.Model;
using Xunit;

namespace CloudNotes.Desktop.Tests;

public class NoteService_CreateNoteAsync_Tests : NoteServiceTestsBase
{
    [Fact]
    public async Task CreateNoteAsync_WithValidNote_ReturnsNoteWithId()
    {
        // Arrange
        var noteToCreate = new Note
        {
            Title = "Test Title",
            Content = "Test Content",
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var createdNote = await _service.CreateNoteAsync(noteToCreate);

        // Assert
        Assert.NotNull(createdNote);
        Assert.NotEqual(Guid.Empty, createdNote.Id);
        Assert.Equal("Test Title", createdNote.Title);
        Assert.Equal("Test Content", createdNote.Content);
    }

    [Fact]
    public async Task CreateNoteAsync_WithValidNote_SavesNoteToDatabase()
    {
        // Arrange
        var noteToCreate = new Note
        {
            Title = "Test Title",
            Content = "Test Content",
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var createdNote = await _service.CreateNoteAsync(noteToCreate);

        // Assert
        var retrievedNote = await _context.Notes.FindAsync(createdNote.Id);
        Assert.NotNull(retrievedNote);
        Assert.Equal(createdNote.Id, retrievedNote.Id);
        Assert.Equal("Test Title", retrievedNote.Title);
        Assert.Equal("Test Content", retrievedNote.Content);
    }
}
