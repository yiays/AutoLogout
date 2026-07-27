using Avalonia.Controls;

namespace AutoLogout;

public partial class AboutWindow : Window
{
    public string Version { get => "v"+State.Current.Version; }
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = this;
    }
}