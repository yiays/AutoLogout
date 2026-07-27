using Avalonia.Controls;

namespace AutoLogout;

public partial class LockoutWindow : Window
{
    public LockoutWindow()
    {
        /// parent should always be provided
        InitializeComponent();
    }
    public LockoutWindow(MainWindow parent)
    {
        InitializeComponent();

        // Add click event to the window
        PointerPressed += (s, e) =>
        {
          parent.Topmost = false;
          parent.Topmost = true;
        };
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        e.Cancel = true;  // Prevents window from closing
    }
}