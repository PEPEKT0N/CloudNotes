using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CloudNotes.Desktop.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CloudNotes.Desktop.Views
{
    public partial class FolderSelectionDialog : Window
    {
        private readonly List<FolderTreeItem> _allFolders;
        private readonly Guid? _currentFolderId;

        public FolderSelectionDialog(List<FolderTreeItem> folders, Guid? currentFolderId)
        {
            InitializeComponent();
            _allFolders = folders;
            _currentFolderId = currentFolderId;

            // Устанавливаем источник данных для ListBox (включая "(No folder)")
            FolderListBox.ItemsSource = folders;

            // Выделяем текущую папку, если она есть
            if (currentFolderId.HasValue)
            {
                var currentFolder = folders.FirstOrDefault(f => f.Id == currentFolderId.Value);
                if (currentFolder != null)
                {
                    FolderListBox.SelectedItem = currentFolder;
                }
            }
            else
            {
                // Выделяем "(No folder)" если заметка не в папке
                var noFolderItem = folders.FirstOrDefault(f => f.Id == Guid.Empty);
                if (noFolderItem != null)
                {
                    FolderListBox.SelectedItem = noFolderItem;
                }
            }

            this.Opened += (_, __) =>
            {
                FolderListBox.Focus();
            };

            OkButton.Click += (_, __) =>
            {
                var selected = FolderListBox.SelectedItem as FolderTreeItem;
                Close(selected);
            };

            CancelButton.Click += (_, __) =>
            {
                Close(null);
            };

            FolderListBox.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    var selected = FolderListBox.SelectedItem as FolderTreeItem;
                    Close(selected);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    Close(null);
                    e.Handled = true;
                }
            };

            FolderListBox.DoubleTapped += (sender, e) =>
            {
                var selected = FolderListBox.SelectedItem as FolderTreeItem;
                if (selected != null)
                {
                    Close(selected);
                    e.Handled = true;
                }
            };
        }


        public static Task<FolderTreeItem?> ShowDialogAsync(Window? owner, List<FolderTreeItem> folders, Guid? currentFolderId)
        {
            var dlg = new FolderSelectionDialog(folders, currentFolderId);
            return dlg.ShowDialog<FolderTreeItem?>(owner!);
        }
    }
}
