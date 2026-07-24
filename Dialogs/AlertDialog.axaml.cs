using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AutoLogout;

public partial class AlertDialog : Window
{
    public string Text {get; set;}
    public string Caption {get; set;}

    public AlertDialog() : this("Unset", "Unset")
    {
        
    }
    public AlertDialog(string text, string caption)
    {
        Text = text;
        Caption = caption;
        InitializeComponent();
        DataContext = this;

        ConfirmButton.AttachedToVisualTree += (s,e) => ConfirmButton.Focus();
        Activated += (s,e) => Console.Beep();
    }

    private void ConfirmButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}