using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AutoLogout;

public partial class PromptDialog : Window
{
    public string? Result { get; private set; }
    public string Text {get; set;}
    public string Caption {get; set;}
    public string Input {get; set;}
    public char? Sensitive {get; set;}

    public PromptDialog()
    {
        Text = "Unset";
        Caption = "Unset";
        Input = "";
        Sensitive = null;
        InitializeComponent();
        DataContext = this;

        Activated += (s,e) => InputTextBox.Focus();
    }
    public PromptDialog(string text, string caption, bool sensitive = false)
    {
        Text = text;
        Caption = caption;
        Input = "";
        Sensitive = sensitive? '*': null;
        InitializeComponent();
        DataContext = this;
    }

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        Result = Input;
        Close();
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }
}