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
    public class TagServiceTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly ITagService _tagService;
        private readonly INoteService _noteService;

        public TagServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _context.Database.EnsureCreated();

            _tagService = new TagService(_context);
            _noteService = new NoteService(_context);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        // -------------------------------------------------------
        // Тесты для CRUD тегов
        // -------------------------------------------------------

        public class TagCrudTests : TagServiceTests
        {
            [Fact]
            public async Task CreateTagAsync_SavesTagToDatabase()
            {
                var tag = new Tag
                {
                    Id = Guid.NewGuid(),
                    Name = "Work"
                };

                var result = await _tagService.CreateTagAsync(tag);

                Assert.NotNull(result);
                Assert.Equal("Work", result.Name);
            }

            [Fact]
            public async Task GetAllTagsAsync_ReturnsAllTags()
            {
                await _tagService.CreateTagAsync(new Tag { Name = "Work" });
                await _tagService.CreateTagAsync(new Tag { Name = "Personal" });
                await _tagService.CreateTagAsync(new Tag { Name = "Urgent" });

                var tags = await _tagService.GetAllTagsAsync();

                Assert.Equal(3, tags.Count());
            }

            [Fact]
            public async Task GetTagByNameAsync_ReturnsTagWhenExists()
            {
                await _tagService.CreateTagAsync(new Tag { Name = "Work" });

                var result = await _tagService.GetTagByNameAsync("Work");

                Assert.NotNull(result);
                Assert.Equal("Work", result!.Name);
            }

            [Fact]
            public async Task GetTagByNameAsync_IsCaseInsensitive()
            {
                await _tagService.CreateTagAsync(new Tag { Name = "Work" });

                var result = await _tagService.GetTagByNameAsync("work");

                Assert.NotNull(result);
                Assert.Equal("Work", result!.Name);
            }

            [Fact]
            public async Task GetTagByNameAsync_ReturnsNullWhenNotExists()
            {
                var result = await _tagService.GetTagByNameAsync("NonExistent");

                Assert.Null(result);
            }

            [Fact]
            public async Task DeleteTagAsync_RemovesTag()
            {
                var tag = await _tagService.CreateTagAsync(new Tag { Name = "ToDelete" });

                var deleteResult = await _tagService.DeleteTagAsync(tag.Id);
                var findResult = await _tagService.GetTagByIdAsync(tag.Id);

                Assert.True(deleteResult);
                Assert.Null(findResult);
            }
        }

        // -------------------------------------------------------
        // Тесты для GetOrCreateTagAsync
        // -------------------------------------------------------

        public class GetOrCreateTests : TagServiceTests
        {
            [Fact]
            public async Task GetOrCreateTagAsync_CreatesNewTagWhenNotExists()
            {
                var tagsBefore = await _tagService.GetAllTagsAsync();
                Assert.Empty(tagsBefore);

                var tag = await _tagService.GetOrCreateTagAsync("NewTag");

                Assert.NotNull(tag);
                Assert.Equal("NewTag", tag.Name);

                var tagsAfter = await _tagService.GetAllTagsAsync();
                Assert.Single(tagsAfter);
            }

            [Fact]
            public async Task GetOrCreateTagAsync_ReturnsExistingTagWhenExists()
            {
                var existingTag = await _tagService.CreateTagAsync(new Tag { Name = "Existing" });

                var result = await _tagService.GetOrCreateTagAsync("Existing");

                Assert.Equal(existingTag.Id, result.Id);
                Assert.Equal("Existing", result.Name);

                // Проверяем, что не создался дубликат
                var allTags = await _tagService.GetAllTagsAsync();
                Assert.Single(allTags);
            }

            [Fact]
            public async Task GetOrCreateTagAsync_IsCaseInsensitive()
            {
                await _tagService.CreateTagAsync(new Tag { Name = "Work" });

                var result = await _tagService.GetOrCreateTagAsync("work");

                Assert.Equal("Work", result.Name);

                // Проверяем, что не создался дубликат
                var allTags = await _tagService.GetAllTagsAsync();
                Assert.Single(allTags);
            }

            [Fact]
            public async Task GetOrCreateTagAsync_TrimsWhitespace()
            {
                var tag = await _tagService.GetOrCreateTagAsync("  Trimmed  ");

                Assert.Equal("Trimmed", tag.Name);
            }
        }

        // -------------------------------------------------------
        // Тесты для связи Note ↔ Tag
        // -------------------------------------------------------

        public class NoteTagRelationTests : TagServiceTests
        {
            [Fact]
            public async Task AddTagToNoteAsync_AddsTagToNote()
            {
                var note = await _noteService.CreateNoteAsync(new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Note",
                    Content = "Content"
                });
                var tag = await _tagService.CreateTagAsync(new Tag { Name = "Important" });

                await _tagService.AddTagToNoteAsync(note.Id, tag.Id);

                var noteTags = await _tagService.GetTagsForNoteAsync(note.Id);
                Assert.Single(noteTags);
                Assert.Equal("Important", noteTags.First().Name);
            }

            [Fact]
            public async Task AddTagToNoteAsync_DoesNotAddDuplicate()
            {
                var note = await _noteService.CreateNoteAsync(new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Note",
                    Content = "Content"
                });
                var tag = await _tagService.CreateTagAsync(new Tag { Name = "Important" });

                // Добавляем тег дважды
                await _tagService.AddTagToNoteAsync(note.Id, tag.Id);
                await _tagService.AddTagToNoteAsync(note.Id, tag.Id);

                // Должен быть только один тег
                var noteTags = await _tagService.GetTagsForNoteAsync(note.Id);
                Assert.Single(noteTags);
            }

            [Fact]
            public async Task RemoveTagFromNoteAsync_RemovesTagFromNote()
            {
                var note = await _noteService.CreateNoteAsync(new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Note",
                    Content = "Content"
                });
                var tag = await _tagService.CreateTagAsync(new Tag { Name = "ToRemove" });

                await _tagService.AddTagToNoteAsync(note.Id, tag.Id);
                var tagsBefore = await _tagService.GetTagsForNoteAsync(note.Id);
                Assert.Single(tagsBefore);

                await _tagService.RemoveTagFromNoteAsync(note.Id, tag.Id);

                var tagsAfter = await _tagService.GetTagsForNoteAsync(note.Id);
                Assert.Empty(tagsAfter);
            }

            [Fact]
            public async Task RemoveTagFromNoteAsync_DoesNotAffectOtherNotes()
            {
                var note1 = await _noteService.CreateNoteAsync(new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Note 1",
                    Content = "Content"
                });
                var note2 = await _noteService.CreateNoteAsync(new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Note 2",
                    Content = "Content"
                });
                var tag = await _tagService.CreateTagAsync(new Tag { Name = "Shared" });

                await _tagService.AddTagToNoteAsync(note1.Id, tag.Id);
                await _tagService.AddTagToNoteAsync(note2.Id, tag.Id);

                // Удаляем тег только из note1
                await _tagService.RemoveTagFromNoteAsync(note1.Id, tag.Id);

                var note1Tags = await _tagService.GetTagsForNoteAsync(note1.Id);
                var note2Tags = await _tagService.GetTagsForNoteAsync(note2.Id);

                Assert.Empty(note1Tags);
                Assert.Single(note2Tags);
            }

            [Fact]
            public async Task GetTagsForNoteAsync_ReturnsMultipleTags()
            {
                var note = await _noteService.CreateNoteAsync(new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Note",
                    Content = "Content"
                });
                var tag1 = await _tagService.CreateTagAsync(new Tag { Name = "Work" });
                var tag2 = await _tagService.CreateTagAsync(new Tag { Name = "Urgent" });
                var tag3 = await _tagService.CreateTagAsync(new Tag { Name = "Project" });

                await _tagService.AddTagToNoteAsync(note.Id, tag1.Id);
                await _tagService.AddTagToNoteAsync(note.Id, tag2.Id);
                await _tagService.AddTagToNoteAsync(note.Id, tag3.Id);

                var noteTags = await _tagService.GetTagsForNoteAsync(note.Id);

                Assert.Equal(3, noteTags.Count());
            }
        }

        // -------------------------------------------------------
        // Тесты каскадного удаления
        // -------------------------------------------------------

        public class CascadeDeleteTests : TagServiceTests
        {
            [Fact]
            public async Task DeleteNote_RemovesNoteTagRelations()
            {
                var note = await _noteService.CreateNoteAsync(new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Note",
                    Content = "Content"
                });
                var tag = await _tagService.CreateTagAsync(new Tag { Name = "TestTag" });

                await _tagService.AddTagToNoteAsync(note.Id, tag.Id);

                // Удаляем заметку
                await _noteService.DeleteNoteAsync(note.Id);

                // Тег должен остаться в БД
                var tagAfterDelete = await _tagService.GetTagByIdAsync(tag.Id);
                Assert.NotNull(tagAfterDelete);

                // Но связь NoteTag должна быть удалена
                var noteTagsCount = await _context.NoteTags.CountAsync();
                Assert.Equal(0, noteTagsCount);
            }

            [Fact]
            public async Task DeleteTag_RemovesNoteTagRelations()
            {
                var note = await _noteService.CreateNoteAsync(new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Test Note",
                    Content = "Content"
                });
                var tag = await _tagService.CreateTagAsync(new Tag { Name = "TestTag" });

                await _tagService.AddTagToNoteAsync(note.Id, tag.Id);

                // Удаляем тег
                await _tagService.DeleteTagAsync(tag.Id);

                // Заметка должна остаться
                var noteAfterDelete = await _noteService.GetNoteByIdAsync(note.Id);
                Assert.NotNull(noteAfterDelete);

                // У заметки не должно быть тегов
                var noteTags = await _tagService.GetTagsForNoteAsync(note.Id);
                Assert.Empty(noteTags);
            }
        }
    }
}
