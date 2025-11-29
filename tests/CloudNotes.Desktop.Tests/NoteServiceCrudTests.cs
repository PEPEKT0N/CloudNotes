using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;

using CloudNotes.Desktop.Data;
using CloudNotes.Desktop.Model;
using CloudNotes.Desktop.Services;

namespace CloudNotes.Desktop.Tests
{
    public class NoteServiceCrudTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly INoteService _noteService;

        public NoteServiceCrudTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _context.Database.EnsureCreated();

            _noteService = new NoteService(_context);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        // Тесты для Create
        public class CreateTests : NoteServiceCrudTests
        {
            [Fact]
            public async Task CreateNoteAsync_SavesNoteToDatabase()
            {
                var note = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Note",
                    Content = "Test Content",
                    UpdatedAt = DateTime.Now
                };

                var result = await _noteService.CreateNoteAsync(note);

                Assert.NotNull(result);
                Assert.Equal(note.Id, result.Id);
                Assert.Equal("Test Note", result.Title);
                Assert.Equal("Test Content", result.Content);
            }

            [Fact]
            public async Task CreateNoteAsync_UpdatesTimestamp()
            {
                var note = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Note",
                    Content = "Test Content",
                    UpdatedAt = DateTime.MinValue
                };

                var beforeCreate = DateTime.Now;
                var result = await _noteService.CreateNoteAsync(note);
                var afterCreate = DateTime.Now;

                Assert.True(result.UpdatedAt >= beforeCreate);
                Assert.True(result.UpdatedAt <= afterCreate);
            }

            [Fact]
            public async Task CreateNoteAsync_CanCreateMultipleNotes()
            {
                var note1 = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Note 1",
                    Content = "Content 1",
                    UpdatedAt = DateTime.Now
                };

                var note2 = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Note 2",
                    Content = "Content 2",
                    UpdatedAt = DateTime.Now
                };

                await _noteService.CreateNoteAsync(note1);
                await _noteService.CreateNoteAsync(note2);

                var allNotes = await _noteService.GetAllNoteAsync();
                Assert.Equal(2, allNotes.Count());
            }
        }

        // Тесты для Read
        public class ReadTests : NoteServiceCrudTests
        {
            [Fact]
            public async Task GetAllNoteAsync_ReturnsEmptyListWhenDatabaseIsEmpty()
            {
                var notes = await _noteService.GetAllNoteAsync();

                Assert.Empty(notes);
            }

            [Fact]
            public async Task GetAllNoteAsync_ReturnsAllNotes()
            {
                var note1 = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Note 1",
                    Content = "Content 1",
                    UpdatedAt = DateTime.Now
                };

                var note2 = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Note 2",
                    Content = "Content 2",
                    UpdatedAt = DateTime.Now
                };

                await _noteService.CreateNoteAsync(note1);
                await _noteService.CreateNoteAsync(note2);

                var allNotes = await _noteService.GetAllNoteAsync();
                var notesList = allNotes.ToList();

                Assert.Equal(2, notesList.Count);
                Assert.Contains(notesList, n => n.Id == note1.Id);
                Assert.Contains(notesList, n => n.Id == note2.Id);
            }

            [Fact]
            public async Task GetNoteByIdAsync_ReturnsNoteWhenExists()
            {
                var note = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Note",
                    Content = "Test Content",
                    UpdatedAt = DateTime.Now
                };

                await _noteService.CreateNoteAsync(note);

                var result = await _noteService.GetNoteByIdAsync(note.Id);

                Assert.NotNull(result);
                Assert.Equal(note.Id, result!.Id);
                Assert.Equal("Test Note", result.Title);
                Assert.Equal("Test Content", result.Content);
            }

            [Fact]
            public async Task GetNoteByIdAsync_ReturnsNullWhenNoteDoesNotExist()
            {
                var nonExistentId = Guid.NewGuid();

                var result = await _noteService.GetNoteByIdAsync(nonExistentId);

                Assert.Null(result);
            }
        }

        // Тесты для Update
        public class UpdateTests : NoteServiceCrudTests
        {
            [Fact]
            public async Task UpdateNoteAsync_UpdatesTitle()
            {
                var note = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Original Title",
                    Content = "Original Content",
                    UpdatedAt = DateTime.Now
                };

                await _noteService.CreateNoteAsync(note);

                note.Title = "Updated Title";
                var result = await _noteService.UpdateNoteAsync(note);

                Assert.True(result);

                var updatedNote = await _noteService.GetNoteByIdAsync(note.Id);
                Assert.NotNull(updatedNote);
                Assert.Equal("Updated Title", updatedNote!.Title);
            }

            [Fact]
            public async Task UpdateNoteAsync_UpdatesContent()
            {
                var note = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Title",
                    Content = "Original Content",
                    UpdatedAt = DateTime.Now
                };

                await _noteService.CreateNoteAsync(note);

                note.Content = "Updated Content";
                var result = await _noteService.UpdateNoteAsync(note);

                Assert.True(result);

                var updatedNote = await _noteService.GetNoteByIdAsync(note.Id);
                Assert.NotNull(updatedNote);
                Assert.Equal("Updated Content", updatedNote!.Content);
            }

            [Fact]
            public async Task UpdateNoteAsync_UpdatesIsFavorite()
            {
                var note = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Title",
                    Content = "Test Content",
                    IsFavorite = false,
                    UpdatedAt = DateTime.Now
                };

                await _noteService.CreateNoteAsync(note);

                note.IsFavorite = true;
                var result = await _noteService.UpdateNoteAsync(note);

                Assert.True(result);

                var updatedNote = await _noteService.GetNoteByIdAsync(note.Id);
                Assert.NotNull(updatedNote);
                Assert.True(updatedNote!.IsFavorite);
            }

            [Fact]
            public async Task UpdateNoteAsync_UpdatesTimestamp()
            {
                var note = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Title",
                    Content = "Test Content",
                    UpdatedAt = DateTime.Now.AddHours(-1)
                };

                await _noteService.CreateNoteAsync(note);
                var originalTimestamp = note.UpdatedAt;

                await Task.Delay(10); // Небольшая задержка для гарантии разницы во времени

                var beforeUpdate = DateTime.Now;
                var result = await _noteService.UpdateNoteAsync(note);
                var afterUpdate = DateTime.Now;

                Assert.True(result);

                var updatedNote = await _noteService.GetNoteByIdAsync(note.Id);
                Assert.NotNull(updatedNote);
                Assert.True(updatedNote!.UpdatedAt >= beforeUpdate);
                Assert.True(updatedNote.UpdatedAt <= afterUpdate);
                Assert.True(updatedNote.UpdatedAt > originalTimestamp);
            }

            [Fact]
            public async Task UpdateNoteAsync_ReturnsFalseWhenNoteDoesNotExist()
            {
                var nonExistentNote = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Non Existent",
                    Content = "Content",
                    UpdatedAt = DateTime.Now
                };

                var result = await _noteService.UpdateNoteAsync(nonExistentNote);

                Assert.False(result);
            }

            [Fact]
            public async Task UpdateNoteAsync_UpdatesAllFields()
            {
                var note = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Original Title",
                    Content = "Original Content",
                    IsFavorite = false,
                    UpdatedAt = DateTime.Now
                };

                await _noteService.CreateNoteAsync(note);

                note.Title = "New Title";
                note.Content = "New Content";
                note.IsFavorite = true;

                var result = await _noteService.UpdateNoteAsync(note);

                Assert.True(result);

                var updatedNote = await _noteService.GetNoteByIdAsync(note.Id);
                Assert.NotNull(updatedNote);
                Assert.Equal("New Title", updatedNote!.Title);
                Assert.Equal("New Content", updatedNote.Content);
                Assert.True(updatedNote.IsFavorite);
            }
        }

        // Тесты для Delete
        public class DeleteTests : NoteServiceCrudTests
        {
            [Fact]
            public async Task DeleteNoteAsync_RemovesNoteFromDatabase()
            {
                var note = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Note",
                    Content = "Test Content",
                    UpdatedAt = DateTime.Now
                };

                await _noteService.CreateNoteAsync(note);

                var result = await _noteService.DeleteNoteAsync(note.Id);

                Assert.True(result);

                var deletedNote = await _noteService.GetNoteByIdAsync(note.Id);
                Assert.Null(deletedNote);
            }

            [Fact]
            public async Task DeleteNoteAsync_ReturnsFalseWhenNoteDoesNotExist()
            {
                var nonExistentId = Guid.NewGuid();

                var result = await _noteService.DeleteNoteAsync(nonExistentId);

                Assert.False(result);
            }

            [Fact]
            public async Task DeleteNoteAsync_DoesNotAffectOtherNotes()
            {
                var note1 = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Note 1",
                    Content = "Content 1",
                    UpdatedAt = DateTime.Now
                };

                var note2 = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Note 2",
                    Content = "Content 2",
                    UpdatedAt = DateTime.Now
                };

                await _noteService.CreateNoteAsync(note1);
                await _noteService.CreateNoteAsync(note2);

                await _noteService.DeleteNoteAsync(note1.Id);

                var allNotes = await _noteService.GetAllNoteAsync();
                var notesList = allNotes.ToList();

                Assert.Single(notesList);
                Assert.Contains(notesList, n => n.Id == note2.Id);
                Assert.DoesNotContain(notesList, n => n.Id == note1.Id);
            }
        }

        // Интеграционные тесты для полного цикла CRUD
        public class CrudIntegrationTests : NoteServiceCrudTests
        {
            [Fact]
            public async Task FullCrudCycle_Create_Read_Update_Delete()
            {
                // Create
                var note = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Note",
                    Content = "Test Content",
                    UpdatedAt = DateTime.Now
                };

                var createdNote = await _noteService.CreateNoteAsync(note);
                Assert.NotNull(createdNote);

                // Read
                var readNote = await _noteService.GetNoteByIdAsync(createdNote.Id);
                Assert.NotNull(readNote);
                Assert.Equal("Test Note", readNote!.Title);

                // Update
                readNote.Title = "Updated Title";
                readNote.Content = "Updated Content";
                var updateResult = await _noteService.UpdateNoteAsync(readNote);
                Assert.True(updateResult);

                var updatedNote = await _noteService.GetNoteByIdAsync(createdNote.Id);
                Assert.NotNull(updatedNote);
                Assert.Equal("Updated Title", updatedNote!.Title);
                Assert.Equal("Updated Content", updatedNote.Content);

                // Delete
                var deleteResult = await _noteService.DeleteNoteAsync(createdNote.Id);
                Assert.True(deleteResult);

                var deletedNote = await _noteService.GetNoteByIdAsync(createdNote.Id);
                Assert.Null(deletedNote);
            }

            [Fact]
            public async Task MultipleNotesCrud_WorksCorrectly()
            {
                // Create multiple notes
                var note1 = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Note 1",
                    Content = "Content 1",
                    UpdatedAt = DateTime.Now
                };

                var note2 = new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Note 2",
                    Content = "Content 2",
                    UpdatedAt = DateTime.Now
                };

                await _noteService.CreateNoteAsync(note1);
                await _noteService.CreateNoteAsync(note2);

                // Read all
                var allNotes = await _noteService.GetAllNoteAsync();
                Assert.Equal(2, allNotes.Count());

                // Update one
                note1.Title = "Updated Note 1";
                await _noteService.UpdateNoteAsync(note1);

                // Verify update
                var updatedNote1 = await _noteService.GetNoteByIdAsync(note1.Id);
                Assert.Equal("Updated Note 1", updatedNote1!.Title);

                // Delete one
                await _noteService.DeleteNoteAsync(note1.Id);

                // Verify deletion
                var remainingNotes = await _noteService.GetAllNoteAsync();
                Assert.Single(remainingNotes);
                Assert.Contains(remainingNotes, n => n.Id == note2.Id);
            }
        }
    }
}

