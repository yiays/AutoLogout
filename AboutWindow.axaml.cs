using Avalonia.Controls;

namespace AutoLogout;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        // Hide instead of closing
        Closing += (s, e) =>
        {
            Hide();
            e.Cancel = true;
        };
    }
}