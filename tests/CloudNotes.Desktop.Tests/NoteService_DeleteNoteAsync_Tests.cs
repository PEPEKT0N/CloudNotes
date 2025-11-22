using System;
using System.Threading.Tasks;
using CloudNotes.Desktop.Model;
using Xunit;

namespace CloudNotes.Desktop.Tests;

public class NoteService_DeleteNoteAsync_Tests : NoteServiceTestsBase
{
    [Fact]
    public async Task DeleteNoteAsync_WithExistingId_DeletesNoteFromDatabase()
    {
        // Arrange
        var noteToDelete = new Note
        {
            Id = Guid.NewGuid(),
            Title = "Note to delete",
            Content = "Content",
            UpdatedAt = DateTime.UtcNow
        };
        _context.Notes.Add(noteToDelete);
        await _context.SaveChangesAsync();

        // Act
        var success = await _service.DeleteNoteAsync(noteToDelete.Id);

        // Assert
        Assert.True(success);
        var deletedNote = await _context.Notes.FindAsync(noteToDelete.Id);
        Assert.Null(deletedNote);
    }

    [Fact]
    public async Task DeleteNoteAsync_WithNonExistingId_ReturnsFalse()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid();

        // Act
        var success = await _service.DeleteNoteAsync(nonExistingId);

        // Assert
        Assert.False(success);
    }
}
