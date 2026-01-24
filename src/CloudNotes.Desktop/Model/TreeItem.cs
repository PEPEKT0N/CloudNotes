using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace CloudNotes.Desktop.Model;

/// <summary>
/// Элемент дерева, который может быть либо папкой, либо заметкой.
/// </summary>
public class TreeItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly Folder? _folder;
    private readonly Note? _note;

    public bool IsFolder => _folder != null;
    public bool IsNote => _note != null;

    public Guid Id => _folder?.Id ?? _note!.Id;
    public string Name => _folder?.Name ?? _note!.Title;
    public string Icon => IsFolder ? "📁" : "📄";

    public Folder? Folder => _folder;
    public Note? Note => _note;

    public ObservableCollection<TreeItem> Children { get; } = new();

    public TreeItem(Folder folder)
    {
        _folder = folder ?? throw new ArgumentNullException(nameof(folder));
    }

    public TreeItem(Note note)
    {
        _note = note ?? throw new ArgumentNullException(nameof(note));
    }

    protected virtual void OnPropertyChanged(string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
