using System;
using System.Threading.Tasks;
using CloudNotes.Desktop.Model;
using Xunit;

namespace CloudNotes.Desktop.Tests;

public class NoteService_GetNoteByIdAsync_Tests : NoteServiceTestsBase
{
    [Fact]
    public async Task GetNoteByIdAsync_WithExistingId_ReturnsCorrectNote()
    {
        // Arrange
        var existingNote = new Note
        {
            Id = Guid.NewGuid(),
            Title = "Existing",
            Content = "Content",
            UpdatedAt = DateTime.UtcNow
        };
        _context.Notes.Add(existingNote);
        await _context.SaveChangesAsync();

        // Act
        var retrievedNote = await _service.GetNoteByIdAsync(existingNote.Id);

        // Assert
        Assert.NotNull(retrievedNote);
        Assert.Equal(existingNote.Id, retrievedNote.Id);
        Assert.Equal(existingNote.Title, retrievedNote.Title);
        Assert.Equal(existingNote.Content, retrievedNote.Content);
    }

    [Fact]
    public async Task GetNoteByIdAsync_WithNonExistingId_ReturnsNull()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid();

        // Act
        var retrievedNote = await _service.GetNoteByIdAsync(nonExistingId);

        // Assert
        Assert.Null(retrievedNote);
    }
}
