using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Threading.Tasks;

namespace CloudNotes.Desktop.Views
{
    public partial class RenameDialog : Window
    {
        public RenameDialog()
        {
            InitializeComponent();

            this.Opened += (_, __) =>
            {
                NameTextBox.Focus();
                NameTextBox.SelectAll();
            };

            OkButton.Click += (_, __) =>
            {
                Close(NameTextBox.Text);
            };

            CancelButton.Click += (_, __) =>
            {
                Close(null);
            };

            NameTextBox.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    Close(NameTextBox.Text);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    Close(null);
                    e.Handled = true;
                }
            };
        }

        public static Task<string?> ShowDialogAsync(Window? owner, string initialText)
        {
            var dlg = new RenameDialog();
            dlg.NameTextBox.Text = initialText ?? string.Empty;
            return dlg.ShowDialog<string?>(owner);
        }
    }
}