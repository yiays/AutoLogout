using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AutoLogout;

public partial class ConfirmDialog : Window
{
    public bool? Result { get; private set; }
    public string Text {get; set;}
    public string Caption {get; set;}

    public ConfirmDialog()
    {
        Text = "Unset";
        Caption = "Unset";
        InitializeComponent();
        DataContext = this;
    }
    public ConfirmDialog(string text, string caption, bool sensitive = false)
    {
        Text = text;
        Caption = caption;
        InitializeComponent();
        DataContext = this;
    }

    private void ConfirmButton_Click(object? sender, RoutedEventArgs e)
    {
        Result = true;
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }
}