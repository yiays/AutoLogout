using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using System;

namespace AutoLogout;

public partial class MainWindow : Window
{
    public State state = new();
    AboutWindow? aboutWindow;
    LockoutWindow? lockoutWindow;
    readonly DispatcherTimer Timer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };
    public MainWindow()
    {
        InitializeComponent();
        Reposition();
        
        // Set tooltips
        ToolTip.SetTip(AboutButton, "Learn more about AutoLogout");
        ToolTip.SetTip(PauseButton, "Pause");
        ToolTip.SetTip(LogoffButton, "Log off");
        ToolTip.SetTip(ShutdownButton, "Shut down");
        ToolTip.SetTip(SettingsButton, "Settings");

        // Click events
        AboutButton.Click += AboutButton_Click;
        PauseButton.Click += PauseButton_Click;
        SettingsButton.Click += SettingsButton_Click;
        LogoffButton.Click += LogoffButton_Click;
        ShutdownButton.Click += ShutdownButton_Click;

        // System events
        ScalingChanged += (o, e) => Reposition();
        Screens.Changed += (o, e) => Reposition();

        // Internal events
        Timer.Tick += state.Tick;
        Timer.Tick += Timer_Tick;
        Timer.Start();
        state.Changed += OnChanged;
    }

    private void OnChanged()
    {
        if(state.Paused)
        {
            PauseButton.Content = "▶️ Resume";
            lockoutWindow ??= new LockoutWindow(this);
            lockoutWindow.Show();
        }
        else
        {
            lockoutWindow?.Hide();
            PauseButton.Content = "⏸️ Pause";
            LabelTimer.Opacity = 1;
            Topmost = false;
            if(state.RemainingTime is null)
            {
                LabelTimer.Text = "Unlimited";
            }else{
                var timeSpan = TimeSpan.FromSeconds((int)state.RemainingTime);
                LabelTimer.Text = string.Format("{0:D2}:{1:D2}.{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
            }
        }
        InvalidateVisual();
        if (state.state.syncAuthor.HasValue)
        {
            OS.Current.Notify(
                "Time limit changed",
                "Your time limit rules have been changed remotely."
            );
            state.state.syncAuthor = null;
        }
    }
    private void Timer_Tick(object? sender, EventArgs? e)
    {
        if (state.Paused)
        {
            LabelTimer.Opacity = LabelTimer.Opacity == 1? 0: 1;
            LabelTimer.InvalidateVisual();
        }
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
    private void PauseButton_Click(object? sender, EventArgs? e)
    {
        state.TogglePause();
    }
    private void SettingsButton_Click(object? sender, EventArgs? e)
    {
        //TODO
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