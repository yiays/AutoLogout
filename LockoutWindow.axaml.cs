using Avalonia.Controls;

namespace AutoLogout;

public partial class LockoutWindow : Window
{
    public LockoutWindow()
    {
        InitializeComponent();
    }

    public LockoutWindow(MainWindow parent)
    {
        InitializeComponent();

        // Set icon if needed
        Icon = new WindowIcon("Resources/icon-light.ico");

        // Add click event to the window
        PointerPressed += (s, e) => parent.ReassertTopMost();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        e.Cancel = true;  // Prevents window from closing
    }
}