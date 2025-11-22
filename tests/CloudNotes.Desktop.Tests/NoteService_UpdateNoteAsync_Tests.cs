using System;
using System.Threading.Tasks;
using CloudNotes.Desktop.Model;
using Xunit;

namespace CloudNotes.Desktop.Tests;

public class NoteService_UpdateNoteAsync_Tests : NoteServiceTestsBase
{
    [Fact]
    public async Task UpdateNoteAsync_WithValidNote_UpdatesNoteInDatabase()
    {
        // Arrange
        var existingNote = new Note
        {
            Id = Guid.NewGuid(),
            Title = "Original Title",
            Content = "Original Content",
            UpdatedAt = DateTime.UtcNow
        };
        _context.Notes.Add(existingNote);
        await _context.SaveChangesAsync();

        // Act
        existingNote.Title = "Updated Title";
        existingNote.Content = "Updated Content";
        existingNote.UpdatedAt = DateTime.UtcNow;
        var success = await _service.UpdateNoteAsync(existingNote);

        // Assert
        Assert.True(success);
        var updatedNote = await _context.Notes.FindAsync(existingNote.Id);
        Assert.NotNull(updatedNote);
        Assert.Equal("Updated Title", updatedNote.Title);
        Assert.Equal("Updated Content", updatedNote.Content);
    }

    [Fact]
    public async Task UpdateNoteAsync_WithNonExistingId_ReturnsFalse()
    {
        // Arrange
        var nonExistingNote = new Note
        {
            Id = Guid.NewGuid(),
            Title = "Non Existing",
            Content = "Content",
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var success = await _service.UpdateNoteAsync(nonExistingNote);

        // Assert
        Assert.False(success);
    }
}
