using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Xunit;

using CloudNotes.Desktop.ViewModel;
using CloudNotes.Desktop.Model;
using CloudNotes.Desktop.Data;
using CloudNotes.Desktop.Services;

namespace CloudNotes.Desktop.Tests
{
    [Collection("Sequential")]
    public class NotesViewModelTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly INoteService _noteService;
        private readonly NotesViewModel vm;

        public NotesViewModelTests()
        {
            // Создаем InMemory базу для каждого теста
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _context.Database.EnsureCreated();

            // Добавляем тестовые заметки в БД
            var now = DateTime.Now;
            _context.Notes.AddRange(
                new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Welcome note",
                    Content = "This is a sample note.",
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new Note
                {
                    Id = Guid.NewGuid(),
                    Title = "Second note",
                    Content = "Another sample note.",
                    CreatedAt = now,
                    UpdatedAt = now
                }
            );
            _context.SaveChanges();

            // Создаем сервис с нашим контекстом
            _noteService = new NoteService(_context);

            // Создаем ViewModel с нашим сервисом
            vm = new NotesViewModel(_noteService);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        // Тесты для CreateNote
        public class CreateNote : NotesViewModelTests
        {
            [Fact]
            public void IncreasesNotesCount()
            {
                var initialCount = vm.AllNotes.Count;

                vm.CreateNote();

                Assert.Equal(initialCount + 1, vm.AllNotes.Count);
                Assert.Equal(initialCount + 1, vm.Notes.Count);
            }

            [Fact]
            public void SelectsNewlyCreatedNote()
            {
                vm.CreateNote();

                Assert.NotNull(vm.SelectedListItem);
                Assert.NotNull(vm.SelectedNote);
                Assert.Equal(vm.SelectedListItem!.Id, vm.SelectedNote!.Id);
            }

            [Fact]
            public void CreatesNoteWithUnnamedTitle()
            {
                vm.CreateNote();

                Assert.Equal("Unnamed", vm.SelectedNote!.Title);
                Assert.Equal("Unnamed", vm.SelectedListItem!.Title);
            }
        }

        // Тесты для контекстного меню: Избранное
        public class FavoritesTests : NotesViewModelTests
        {
            [Fact]
            public void AddToFavorites_AddsSelectedNoteToFavorites()
            {
                Assert.NotEmpty(vm.Notes);
                var firstNote = vm.Notes.First();
                vm.SelectedListItem = firstNote;

                vm.AddToFavoritesCommand.Execute(null);

                Assert.NotEmpty(vm.Favorites);
                Assert.Contains(vm.Favorites, f => f.Id == firstNote.Id);
            }

            [Fact]
            public void RemoveFromFavorites_RemovesNoteFromFavorites()
            {
                Assert.NotEmpty(vm.Notes);
                var firstNote = vm.Notes.First();
                vm.SelectedListItem = firstNote;
                vm.AddToFavoritesCommand.Execute(null);
                Assert.NotEmpty(vm.Favorites);

                var favoriteItem = vm.Favorites.First(f => f.Id == firstNote.Id);
                vm.SelectedFavoriteItem = favoriteItem;
                vm.RemoveFromFavoritesCommand.Execute(null);

                Assert.DoesNotContain(vm.Favorites, f => f.Id == firstNote.Id);
            }

            [Fact]
            public void AddToFavorites_SetsIsFavoriteFlag()
            {
                Assert.NotEmpty(vm.Notes);
                var firstNote = vm.Notes.First();
                vm.SelectedListItem = firstNote;
                var noteId = firstNote.Id;

                vm.AddToFavoritesCommand.Execute(null);

                var note = vm.AllNotes.First(n => n.Id == noteId);
                Assert.True(note.IsFavorite);
            }

            [Fact]
            public void RemoveFromFavorites_ClearsIsFavoriteFlag()
            {
                Assert.NotEmpty(vm.Notes);
                var firstNote = vm.Notes.First();
                vm.SelectedListItem = firstNote;
                var noteId = firstNote.Id;
                vm.AddToFavoritesCommand.Execute(null);

                var favoriteItem = vm.Favorites.First(f => f.Id == noteId);
                vm.SelectedFavoriteItem = favoriteItem;
                vm.RemoveFromFavoritesCommand.Execute(null);

                var note = vm.AllNotes.First(n => n.Id == noteId);
                Assert.False(note.IsFavorite);
            }
        }

        // Тесты для контекстного меню: Переименование
        public class RenameTests : NotesViewModelTests
        {
            [Fact]
            public void RenameActiveNote_ChangesNoteTitle()
            {
                Assert.NotEmpty(vm.Notes);
                var firstNote = vm.Notes.First();
                vm.SelectedListItem = firstNote;
                var noteId = firstNote.Id;

                vm.RenameActiveNote("Hello World");

                var updatedNote = vm.Notes.First(n => n.Id == noteId);
                Assert.Equal("Hello World", updatedNote.Title);

                var note = vm.AllNotes.First(n => n.Id == noteId);
                Assert.Equal("Hello World", note.Title);
            }

            [Fact]
            public void RenameActiveNote_UpdatesTimestamp()
            {
                Assert.NotEmpty(vm.Notes);
                var firstNote = vm.Notes.First();
                vm.SelectedListItem = firstNote;
                var noteId = firstNote.Id;
                var originalTime = vm.AllNotes.First(n => n.Id == noteId).UpdatedAt;

                System.Threading.Thread.Sleep(10);

                vm.RenameActiveNote("New Title");

                var note = vm.AllNotes.First(n => n.Id == noteId);
                Assert.True(note.UpdatedAt > originalTime);
            }

            [Fact]
            public void RenameActiveNote_UpdatesFavoriteItemTitle()
            {
                vm.SelectedListItem = vm.Notes[0];
                var noteId = vm.Notes[0].Id;
                vm.AddToFavoritesCommand.Execute(null);

                vm.RenameActiveNote("Renamed Note");

                var favoriteItem = vm.Favorites.First(f => f.Id == noteId);
                Assert.Equal("Renamed Note", favoriteItem.Title);
            }
        }

        // Тесты для контекстного меню: Удаление
        public class DeleteTests : NotesViewModelTests
        {
            [Fact]
            public void DeleteNote_RemovesNoteFromAllCollections()
            {
                Assert.NotEmpty(vm.Notes);
                var initialCount = vm.AllNotes.Count;
                var firstNote = vm.Notes.First();
                vm.SelectedListItem = firstNote;
                var deletedId = firstNote.Id;

                vm.DeleteNoteCommand.Execute(null);

                Assert.Equal(initialCount - 1, vm.AllNotes.Count);
                Assert.Equal(initialCount - 1, vm.Notes.Count);

                Assert.DoesNotContain(vm.AllNotes, n => n.Id == deletedId);
                Assert.DoesNotContain(vm.Notes, n => n.Id == deletedId);
            }

            [Fact]
            public void DeleteNote_RemovesFromFavoritesIfPresent()
            {
                var initialFavoritesCount = vm.Favorites.Count;
                vm.SelectedListItem = vm.Notes[0];
                var deletedId = vm.Notes[0].Id;
                vm.AddToFavoritesCommand.Execute(null);
                Assert.Equal(initialFavoritesCount + 1, vm.Favorites.Count);

                vm.DeleteNoteCommand.Execute(null);

                Assert.Equal(initialFavoritesCount, vm.Favorites.Count);
                Assert.DoesNotContain(vm.Favorites, f => f.Id == deletedId);
            }

            [Fact]
            public void DeleteNote_ClearsSelection()
            {
                Assert.NotEmpty(vm.Notes);
                var firstNote = vm.Notes.First();
                vm.SelectedListItem = firstNote;

                vm.DeleteNoteCommand.Execute(null);

                Assert.Null(vm.SelectedListItem);
                Assert.Null(vm.SelectedNote);
            }

            [Fact]
            public void DeleteActiveNote_WorksWithActiveListItem()
            {
                Assert.NotEmpty(vm.Notes);
                var initialCount = vm.AllNotes.Count;
                var firstNote = vm.Notes.First();
                vm.SelectedListItem = firstNote;
                var deletedId = firstNote.Id;

                vm.DeleteActiveNote();

                Assert.Equal(initialCount - 1, vm.AllNotes.Count);
                Assert.DoesNotContain(vm.AllNotes, n => n.Id == deletedId);
            }
        }

        // Тесты для горячих клавиш (сценарии)
        public class HotkeysScenarioTests : NotesViewModelTests
        {
            [Fact]
            public void CtrlN_CreatesNewNoteWithUnnamedTitle()
            {
                var initialCount = vm.Notes.Count;

                vm.CreateNote();

                Assert.Equal(initialCount + 1, vm.Notes.Count);
                Assert.Equal("Unnamed", vm.SelectedNote!.Title);
            }

            [Fact]
            public void CtrlR_RenamesSelectedNote()
            {
                Assert.NotEmpty(vm.Notes);
                var firstNote = vm.Notes.First();
                vm.SelectedListItem = firstNote;

                vm.RenameActiveNote("Test Note");

                Assert.Equal("Test Note", vm.SelectedListItem.Title);
                Assert.Equal("Test Note", vm.SelectedNote!.Title);
            }

            [Fact]
            public void CtrlS_SavesContentChanges()
            {
                vm.CreateNote();
                var noteId = vm.SelectedNote!.Id;

                vm.SelectedNote.Content = "Hello CloudNotes app";

                var note = vm.AllNotes.First(n => n.Id == noteId);
                Assert.Equal("Hello CloudNotes app", note.Content);
            }

            [Fact]
            public void CtrlD_DeletesSelectedNote()
            {
                Assert.NotEmpty(vm.Notes);
                var initialCount = vm.Notes.Count;
                var firstNote = vm.Notes.First();
                vm.SelectedListItem = firstNote;
                var deletedId = firstNote.Id;

                vm.DeleteActiveNote();

                Assert.Equal(initialCount - 1, vm.Notes.Count);
                Assert.DoesNotContain(vm.AllNotes, n => n.Id == deletedId);
            }
        }

        // Полный сценарий: контекстное меню
        public class ContextMenuFullScenario : NotesViewModelTests
        {
            [Fact]
            public void FullScenario_AddRemoveFavorites_Rename_Delete()
            {
                Assert.NotEmpty(vm.Notes);
                var initialNotesCount = vm.Notes.Count;
                var initialFavoritesCount = vm.Favorites.Count;
                var firstNote = vm.Notes.First();
                vm.SelectedListItem = firstNote;
                var testNoteId = firstNote.Id;

                vm.AddToFavoritesCommand.Execute(null);
                Assert.Equal(initialFavoritesCount + 1, vm.Favorites.Count);

                var favoriteItem = vm.Favorites.First(f => f.Id == testNoteId);
                vm.SelectedFavoriteItem = favoriteItem;
                vm.RemoveFromFavoritesCommand.Execute(null);
                Assert.Equal(initialFavoritesCount, vm.Favorites.Count);

                vm.SelectedListItem = vm.Notes.First(n => n.Id == testNoteId);
                vm.RenameActiveNote("Hello World");
                Assert.Equal("Hello World", vm.Notes.First(n => n.Id == testNoteId).Title);

                vm.DeleteNoteCommand.Execute(null);
                Assert.Equal(initialNotesCount - 1, vm.Notes.Count);
                Assert.DoesNotContain(vm.Notes, n => n.Id == testNoteId);
            }
        }

        // Полный сценарий: горячие клавиши
        public class HotkeysFullScenario : NotesViewModelTests
        {
            [Fact]
            public void FullScenario_Create_Rename_EditContent_Delete()
            {
                var initialCount = vm.Notes.Count;

                vm.CreateNote();
                Assert.Equal(initialCount + 1, vm.Notes.Count);
                Assert.Equal("Unnamed", vm.SelectedNote!.Title);
                var newNoteId = vm.SelectedNote.Id;

                vm.RenameActiveNote("Test Note");
                Assert.Equal("Test Note", vm.SelectedNote.Title);

                vm.SelectedNote.Content = "Hello CloudNotes app";
                var note = vm.AllNotes.First(n => n.Id == newNoteId);
                Assert.Equal("Hello CloudNotes app", note.Content);

                vm.DeleteActiveNote();
                Assert.Equal(initialCount, vm.Notes.Count);
                Assert.DoesNotContain(vm.AllNotes, n => n.Id == newNoteId);
            }
        }

        // Тесты для сортировки
        public class SortingTests : NotesViewModelTests
        {
            [Fact]
            public void SortByTitleAsc_SortsNotesAlphabetically()
            {
                // Создаём заметки с разными названиями
                vm.CreateNote();
                vm.RenameActiveNote("Zebra");
                vm.CreateNote();
                vm.RenameActiveNote("Apple");
                vm.CreateNote();
                vm.RenameActiveNote("Mango");

                // Переключаем на другую сортировку и обратно чтобы применить
                vm.SelectedSortOption = SortOption.TitleDesc;
                vm.SelectedSortOption = SortOption.TitleAsc;

                // Проверяем порядок (A-Z)
                var titles = vm.Notes.Select(n => n.Title).ToList();
                var sortedTitles = titles.OrderBy(t => t).ToList();
                Assert.Equal(sortedTitles, titles);
            }

            [Fact]
            public void SortByTitleDesc_SortsNotesReverseAlphabetically()
            {
                vm.CreateNote();
                vm.RenameActiveNote("Apple");
                vm.CreateNote();
                vm.RenameActiveNote("Zebra");
                vm.CreateNote();
                vm.RenameActiveNote("Mango");

                vm.SelectedSortOption = SortOption.TitleDesc;

                // Проверяем порядок (Z-A)
                var titles = vm.Notes.Select(n => n.Title).ToList();
                var sortedTitles = titles.OrderByDescending(t => t).ToList();
                Assert.Equal(sortedTitles, titles);
            }

            [Fact]
            public void SortByUpdatedAsc_SortsOldestFirst()
            {
                vm.CreateNote();
                vm.RenameActiveNote("First");
                System.Threading.Thread.Sleep(20);
                vm.CreateNote();
                vm.RenameActiveNote("Second");
                System.Threading.Thread.Sleep(20);
                vm.CreateNote();
                vm.RenameActiveNote("Third");

                vm.SelectedSortOption = SortOption.UpdatedAsc;

                // Проверяем что старые сначала
                var timestamps = vm.Notes.Select(n => n.UpdatedAt).ToList();
                var sortedTimestamps = timestamps.OrderBy(t => t).ToList();
                Assert.Equal(sortedTimestamps, timestamps);
            }

            [Fact]
            public void SortByUpdatedDesc_SortsNewestFirst()
            {
                vm.CreateNote();
                vm.RenameActiveNote("First");
                System.Threading.Thread.Sleep(20);
                vm.CreateNote();
                vm.RenameActiveNote("Second");
                System.Threading.Thread.Sleep(20);
                vm.CreateNote();
                vm.RenameActiveNote("Third");

                vm.SelectedSortOption = SortOption.UpdatedDesc;

                // Проверяем что новые сначала
                var timestamps = vm.Notes.Select(n => n.UpdatedAt).ToList();
                var sortedTimestamps = timestamps.OrderByDescending(t => t).ToList();
                Assert.Equal(sortedTimestamps, timestamps);
            }

            [Fact]
            public void SortingAlsoAppliesToFavorites()
            {
                vm.CreateNote();
                vm.RenameActiveNote("Zebra");
                vm.AddToFavoritesCommand.Execute(null);

                vm.CreateNote();
                vm.RenameActiveNote("Apple");
                vm.AddToFavoritesCommand.Execute(null);

                // Переключаем на другую сортировку и обратно
                vm.SelectedSortOption = SortOption.TitleDesc;
                vm.SelectedSortOption = SortOption.TitleAsc;

                var favoriteTitles = vm.Favorites.Select(f => f.Title).ToList();
                var sortedTitles = favoriteTitles.OrderBy(t => t).ToList();
                Assert.Equal(sortedTitles, favoriteTitles);
            }

            [Fact]
            public void ChangingSortOption_ResortsImmediately()
            {
                vm.CreateNote();
                vm.RenameActiveNote("Zebra");
                vm.CreateNote();
                vm.RenameActiveNote("Apple");

                // Сначала Z-A
                vm.SelectedSortOption = SortOption.TitleDesc;
                Assert.Equal("Zebra", vm.Notes.First().Title);

                // Потом A-Z
                vm.SelectedSortOption = SortOption.TitleAsc;
                Assert.Equal("Apple", vm.Notes.First().Title);
            }

            [Fact]
            public void DefaultSortOption_IsTitleAsc()
            {
                Assert.Equal(SortOption.TitleAsc, vm.SelectedSortOption);
            }

            [Fact]
            public void SortOptions_ContainsAllOptions()
            {
                var options = vm.SortOptions;

                Assert.Contains(SortOption.TitleAsc, options);
                Assert.Contains(SortOption.TitleDesc, options);
                Assert.Contains(SortOption.CreatedDesc, options);
                Assert.Contains(SortOption.CreatedAsc, options);
                Assert.Contains(SortOption.UpdatedAsc, options);
                Assert.Contains(SortOption.UpdatedDesc, options);
                Assert.Equal(6, options.Length);
            }

            [Fact]
            public void SortByCreatedDesc_SortsNewestFirst()
            {
                vm.CreateNote();
                vm.RenameActiveNote("First");
                System.Threading.Thread.Sleep(20);
                vm.CreateNote();
                vm.RenameActiveNote("Second");
                System.Threading.Thread.Sleep(20);
                vm.CreateNote();
                vm.RenameActiveNote("Third");

                vm.SelectedSortOption = SortOption.CreatedDesc;

                // Проверяем что новые сначала
                var timestamps = vm.Notes.Select(n => n.CreatedAt).ToList();
                var sortedTimestamps = timestamps.OrderByDescending(t => t).ToList();
                Assert.Equal(sortedTimestamps, timestamps);
            }

            [Fact]
            public void SortByCreatedAsc_SortsOldestFirst()
            {
                vm.CreateNote();
                vm.RenameActiveNote("First");
                System.Threading.Thread.Sleep(20);
                vm.CreateNote();
                vm.RenameActiveNote("Second");
                System.Threading.Thread.Sleep(20);
                vm.CreateNote();
                vm.RenameActiveNote("Third");

                vm.SelectedSortOption = SortOption.CreatedAsc;

                // Проверяем что старые сначала
                var timestamps = vm.Notes.Select(n => n.CreatedAt).ToList();
                var sortedTimestamps = timestamps.OrderBy(t => t).ToList();
                Assert.Equal(sortedTimestamps, timestamps);
            }
        }
    }
}
