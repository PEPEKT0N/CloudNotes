using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using CloudNotes.Desktop.ViewModel;
using System;
using System.Threading.Tasks;

namespace CloudNotes.Desktop.Views;

public partial class NoteListView : UserControl
{
    public NoteListView()
    {
        InitializeComponent();

        this.AttachedToVisualTree += (s, e) => this.Focus();
    }

    private async void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ViewModel.NotesViewModel vm)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.N)
        {
            await SafeInvokeAsync(vm.CreateNewNoteAsync);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.S)
        {
            await SafeInvokeAsync(vm.SaveActiveNoteAsync);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.D)
        {
            await SafeInvokeAsync(vm.DeleteActiveNoteAsync);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.R)
        {
            e.Handled = true;
            await ShowRenameDialogAndRenameAsync(vm);
            return;
        }
    } // OnKeyDown

    private static async Task SafeInvokeAsync(Func<Task> actor)
    {
        try
        {
            if (actor != null)
            {
                await actor();
            }
        }
        catch
        {
            // Logging errors
        }
    }

    private async Task ShowRenameDialogAndRenameAsync(NotesViewModel vm)
    {
        if (vm.ActiveNote == null)
        {
            return;
        }

        var dialog = new Window
        {
            Width = 360,
            Height = 120,
            Title = "Rename note",
            CanResize = false
        };

        var tb = new TextBox
        {
            Text = vm.ActiveNote.Title ?? string.Empty,
            Margin = new Thickness(8)
        };

        var ok = new Button { Content = "OK", IsDefault = true, Margin = new Thickness(8) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(8) };

        var panel = new StackPanel { Margin = new Thickness(8) };
        panel.Children.Add(new TextBlock { Text = "New name:" });
        panel.Children.Add(tb);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        dialog.Content = panel;

        ok.Click += async (_, __) =>
        {
            dialog.Close(tb.Text);
        };
        cancel.Click += (_, __) => dialog.Close(null);

        // Покажем модально относительно текущего окна (если доступно)
        var top = this.VisualRoot as Window;
        var result = await dialog.ShowDialog<string?>(top);

        if (!string.IsNullOrWhiteSpace(result))
        {
            await vm.RenameActiveNoteAsync(result);
        }
    }
}
