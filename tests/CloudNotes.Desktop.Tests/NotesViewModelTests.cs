using System;
using Xunit;

using CloudNotes.Desktop.ViewModel;
using CloudNotes.Desktop.Services;


namespace CloudNotes.Desktop.Tests
{
    public class NotesViewModelTests
    {
        private readonly NotesViewModel vm;

        public NotesViewModelTests()
        {
            vm = new NotesViewModel();
        }

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
        } // class CreateNote        
    } // class NotesViewModelTests
} // namespace CloudNotes.Desktop.Tests
