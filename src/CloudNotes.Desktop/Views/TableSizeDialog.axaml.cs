using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CloudNotes.Desktop.Views;

public partial class TableSizeDialog : Window
{
    public int Rows { get; private set; }
    public int Columns { get; private set; }
    public bool IsInserted { get; private set; }

    public TableSizeDialog()
    {
        InitializeComponent();
        Rows = 3;
        Columns = 3;
    }

    private void OnInsertClick(object? sender, RoutedEventArgs e)
    {
        if (RowsUpDown != null && ColumnsUpDown != null)
        {
            Rows = (int)RowsUpDown.Value;
            Columns = (int)ColumnsUpDown.Value;
            IsInserted = true;
        }
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        IsInserted = false;
        Close();
    }
}
