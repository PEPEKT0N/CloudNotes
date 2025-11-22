using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudNotes.Desktop.Model;
using Xunit;

namespace CloudNotes.Desktop.Tests;

public class NoteService_GetAllNoteAsync_Tests : NoteServiceTestsBase
{
    [Fact]
    public async Task GetAllNoteAsync_WithNoNotes_ReturnsEmptyList()
    {
        // Act
        var notes = await _service.GetAllNoteAsync();

        // Assert
        Assert.NotNull(notes);
        Assert.Empty(notes);
    }

    [Fact]
    public async Task GetAllNoteAsync_WithExistingNotes_ReturnsAllNotes()
    {
        // Arrange
        var note1 = new Note { Id = Guid.NewGuid(), Title = "Note 1", Content = "Content 1", UpdatedAt = DateTime.UtcNow };
        var note2 = new Note { Id = Guid.NewGuid(), Title = "Note 2", Content = "Content 2", UpdatedAt = DateTime.UtcNow };
        _context.Notes.Add(note1);
        _context.Notes.Add(note2);
        await _context.SaveChangesAsync();

        // Act
        var notes = await _service.GetAllNoteAsync();

        // Assert
        Assert.NotNull(notes);
        Assert.Equal(2, notes.Count());
        Assert.Contains(notes, n => n.Id == note1.Id);
        Assert.Contains(notes, n => n.Id == note2.Id);
    }
}
