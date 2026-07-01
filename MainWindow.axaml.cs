using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using System;

namespace AutoLogout;

public partial class MainWindow : Window
{
    public State state = new();
    AboutWindow? aboutWindow;
    public MainWindow()
    {
        InitializeComponent();
        
        // Set tooltips
        ToolTip.SetTip(AboutButton, "Learn more about AutoLogout");
        ToolTip.SetTip(PauseButton, "Pause");
        ToolTip.SetTip(LogoffButton, "Log off");
        ToolTip.SetTip(ShutdownButton, "Shut down");
        ToolTip.SetTip(SettingsButton, "Settings");

        // Click events
        AboutButton.Click += AboutButton_Click;

        Reposition();
        ScalingChanged += Reposition;
        Screens.Changed += Reposition;
    }
    
    private void Reposition(object? sender, EventArgs? e)
    {
        Reposition();
    }
    private void Reposition()
    {
        if (Screens.All.Count > 0)
        {
            var primary = Screens.All[0];
            var width = (int)(Width * DesktopScaling);
            var height = (int)(Height * DesktopScaling);
            Position = new PixelPoint(primary.WorkingArea.Right - width, primary.WorkingArea.Bottom - height);
        }
    }

    private void AboutButton_Click(object? sender, EventArgs? e)
    {
        aboutWindow ??= new AboutWindow();
        aboutWindow.Show();
    }
    private void LogoffButton_Click(object? sender, EventArgs? e)
    {
        OS.Current.SaveState(state.state);
        Timer.Stop();
        state.userIntent = UserIntent.Exit;
        OS.Current.Logoff();
        
    }
    private void ShutdownButton_Click(object? sender, EventArgs? e)
    {
        OS.Current.SaveState(state.state);
        Timer.Stop();
        state.userIntent = UserIntent.Exit;
        OS.Current.Shutdown();
        Close();
    }
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (state.userIntent != UserIntent.Exit)
            e.Cancel = true;
        else
        {
            // Release any held resources before closing
        }
    }
}