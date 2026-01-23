using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace CloudNotes.Desktop.Model;

/// <summary>
/// Элемент дерева папок для отображения в TreeView.
/// </summary>
public class FolderTreeItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private Folder _folder;
    public Folder Folder
    {
        get => _folder;
        set
        {
            if (_folder != value)
            {
                _folder = value;
                OnPropertyChanged();
            }
        }
    }

    public Guid Id => _folder.Id;
    public string Name => _folder.Name;
    public Guid? ParentFolderId => _folder.ParentFolderId;

    public ObservableCollection<FolderTreeItem> Children { get; } = new();

    public FolderTreeItem(Folder folder)
    {
        _folder = folder ?? throw new ArgumentNullException(nameof(folder));
    }

    protected virtual void OnPropertyChanged(string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
