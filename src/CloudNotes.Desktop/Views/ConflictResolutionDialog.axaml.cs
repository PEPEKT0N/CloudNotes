using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using CloudNotes.Desktop.Model;

namespace CloudNotes.Desktop.Views;

public partial class ConflictResolutionDialog : Window
{
    private NoteConflict? _conflict;

    public ConflictResolutionDialog()
    {
        InitializeComponent();

        UseLocalButton.Click += (_, __) => Close(true);
        UseServerButton.Click += (_, __) => Close(false);
        CancelButton.Click += (_, __) => Close(null);
    }

    public void SetConflict(NoteConflict conflict)
    {
        _conflict = conflict ?? throw new ArgumentNullException(nameof(conflict));

        // Local version
        LocalTitleTextBlock.Text = conflict.LocalNote.Title;
        LocalContentTextBox.Text = conflict.LocalNote.Content ?? string.Empty;
        LocalUpdatedTextBlock.Text = conflict.LocalNote.UpdatedAt.ToString("g");

        // Server version
        ServerTitleTextBlock.Text = conflict.ServerNote.Title;
        ServerContentTextBox.Text = conflict.ServerNote.Content ?? string.Empty;
        ServerUpdatedTextBlock.Text = conflict.ServerNote.UpdatedAt.ToString("g");
    }

    // Показать диалог разрешения конфликта
    // Возвращает: true = использовать локальную версию, false = использовать серверную версию, null = отмена
    public static Task<bool?> ShowDialogAsync(Window? owner, NoteConflict conflict)
    {
        var dialog = new ConflictResolutionDialog();
        dialog.SetConflict(conflict);
        return dialog.ShowDialog<bool?>(owner!);
    }
}

