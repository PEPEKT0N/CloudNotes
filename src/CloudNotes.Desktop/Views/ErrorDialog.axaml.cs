using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CloudNotes.Desktop.Views;

public partial class ErrorDialog : Window
{
    public ErrorDialog()
    {
        InitializeComponent();
        
        var okButton = this.FindControl<Button>("OkButton");
        if (okButton != null)
        {
            okButton.Click += OnOkClick;
        }
    }
    
    public ErrorDialog(string message) : this()
    {
        var messageText = this.FindControl<TextBlock>("MessageText");
        if (messageText != null)
        {
            messageText.Text = message;
        }
    }
    
    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
