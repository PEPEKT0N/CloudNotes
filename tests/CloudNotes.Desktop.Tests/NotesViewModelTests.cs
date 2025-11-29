using System;
using System.Linq;
using Xunit;

using CloudNotes.Desktop.ViewModel;
using CloudNotes.Desktop.Model;

namespace CloudNotes.Desktop.Tests
{
    public class NotesViewModelTests
    {
        private readonly NotesViewModel vm;

        public NotesViewModelTests()
        {
            vm = new NotesViewModel();
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
                vm.SelectedListItem = vm.Notes[0];
                
                vm.AddToFavoritesCommand.Execute(null);
                
                Assert.Single(vm.Favorites);
                Assert.Equal(vm.Notes[0].Id, vm.Favorites[0].Id);
            }

            [Fact]
            public void RemoveFromFavorites_RemovesNoteFromFavorites()
            {
                vm.SelectedListItem = vm.Notes[0];
                vm.AddToFavoritesCommand.Execute(null);
                Assert.Single(vm.Favorites);

                vm.SelectedFavoriteItem = vm.Favorites[0];
                vm.RemoveFromFavoritesCommand.Execute(null);

                Assert.Empty(vm.Favorites);
            }

            [Fact]
            public void AddToFavorites_SetsIsFavoriteFlag()
            {
                vm.SelectedListItem = vm.Notes[0];
                var noteId = vm.Notes[0].Id;

                vm.AddToFavoritesCommand.Execute(null);

                var note = vm.AllNotes.First(n => n.Id == noteId);
                Assert.True(note.IsFavorite);
            }

            [Fact]
            public void RemoveFromFavorites_ClearsIsFavoriteFlag()
            {
                // Добавляем и удаляем из избранного
                vm.SelectedListItem = vm.Notes[0];
                var noteId = vm.Notes[0].Id;
                vm.AddToFavoritesCommand.Execute(null);

                vm.SelectedFavoriteItem = vm.Favorites[0];
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
                vm.SelectedListItem = vm.Notes[0];
                var noteId = vm.Notes[0].Id;
                
                vm.RenameActiveNote("Hello World");

                Assert.Equal("Hello World", vm.Notes[0].Title);

                var note = vm.AllNotes.First(n => n.Id == noteId);
                Assert.Equal("Hello World", note.Title);
            }

            [Fact]
            public void RenameActiveNote_UpdatesTimestamp()
            {
                vm.SelectedListItem = vm.Notes[0];
                var noteId = vm.Notes[0].Id;
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
                vm.AddToFavoritesCommand.Execute(null);
                
                vm.RenameActiveNote("Renamed Note");
                
                Assert.Equal("Renamed Note", vm.Favorites[0].Title);
            }
        }

        // Тесты для контекстного меню: Удаление
        public class DeleteTests : NotesViewModelTests
        {
            [Fact]
            public void DeleteNote_RemovesNoteFromAllCollections()
            {
                var initialCount = vm.AllNotes.Count;
                vm.SelectedListItem = vm.Notes[0];
                var deletedId = vm.Notes[0].Id;

                vm.DeleteNoteCommand.Execute(null);

                Assert.Equal(initialCount - 1, vm.AllNotes.Count);
                Assert.Equal(initialCount - 1, vm.Notes.Count);

                Assert.DoesNotContain(vm.AllNotes, n => n.Id == deletedId);
                Assert.DoesNotContain(vm.Notes, n => n.Id == deletedId);
            }

            [Fact]
            public void DeleteNote_RemovesFromFavoritesIfPresent()
            {
                vm.SelectedListItem = vm.Notes[0];
                var deletedId = vm.Notes[0].Id;
                vm.AddToFavoritesCommand.Execute(null);
                Assert.Single(vm.Favorites);

                vm.DeleteNoteCommand.Execute(null);
                
                Assert.Empty(vm.Favorites);
                Assert.DoesNotContain(vm.Favorites, f => f.Id == deletedId);
            }

            [Fact]
            public void DeleteNote_ClearsSelection()
            {
                vm.SelectedListItem = vm.Notes[0];

                vm.DeleteNoteCommand.Execute(null);

                Assert.Null(vm.SelectedListItem);
                Assert.Null(vm.SelectedNote);
            }

            [Fact]
            public void DeleteActiveNote_WorksWithActiveListItem()
            {
                var initialCount = vm.AllNotes.Count;
                vm.SelectedListItem = vm.Notes[0];
                var deletedId = vm.Notes[0].Id;

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
                var initialCount = vm.Notes.Count; // 2 дефолтные

                // Эмулируем Ctrl+N
                vm.CreateNote();

                Assert.Equal(initialCount + 1, vm.Notes.Count);
                Assert.Equal("Unnamed", vm.SelectedNote!.Title);
            }

            [Fact]
            public void CtrlR_RenamesSelectedNote()
            {
                vm.SelectedListItem = vm.Notes[0];

                // Эмулируем Ctrl+R с вводом "Test Note"
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
                var initialCount = vm.Notes.Count;
                vm.SelectedListItem = vm.Notes[0];
                var deletedId = vm.Notes[0].Id;

                vm.DeleteActiveNote();

                Assert.Equal(initialCount - 1, vm.Notes.Count);
                Assert.DoesNotContain(vm.AllNotes, n => n.Id == deletedId);
            }
        }
        
        // контекстное меню        
        public class ContextMenuFullScenario : NotesViewModelTests
        {
            [Fact]
            public void FullScenario_AddRemoveFavorites_Rename_Delete()
            {
                vm.SelectedListItem = vm.Notes[0];
                var welcomeNoteId = vm.Notes[0].Id;

                vm.AddToFavoritesCommand.Execute(null);
                Assert.Single(vm.Favorites);

                vm.SelectedFavoriteItem = vm.Favorites[0];
                vm.RemoveFromFavoritesCommand.Execute(null);
                Assert.Empty(vm.Favorites);

                vm.SelectedListItem = vm.Notes.First(n => n.Id == welcomeNoteId);
                vm.RenameActiveNote("Hello World");
                Assert.Equal("Hello World", vm.Notes.First(n => n.Id == welcomeNoteId).Title);

                vm.DeleteNoteCommand.Execute(null);
                Assert.Single(vm.Notes); 
                Assert.DoesNotContain(vm.Notes, n => n.Id == welcomeNoteId);
            }
        }

        // горячие клавиши        
        public class HotkeysFullScenario : NotesViewModelTests
        {
            [Fact]
            public void FullScenario_Create_Rename_EditContent_Delete()
            {
                var initialCount = vm.Notes.Count; // 2

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
    }
}
