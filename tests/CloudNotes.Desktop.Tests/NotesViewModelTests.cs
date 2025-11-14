using System;
using Xunit;
using CloudNotes.Desktop.ViewModel;

namespace CloudNotes.Desktop.Tests
{
    public class NotesViewModelTests
    {
        [Fact]
        public void CreateNote_AddsNoteToCollectionAndSelectsIt()
        {
            var vm = new NotesViewModel();

            vm.CreateNote();

            Assert.Single(vm.AllNotes);
            Assert.Single(vm.Notes);
            Assert.NotNull(vm.SelectedListItem);
            Assert.NotNull(vm.SelectedNote);
            Assert.Equal(vm.SelectedListItem!.Id, vm.SelectedNote!.Id);
        }
    }
}
