using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using NetCoreAudio;

namespace AutoLogout;

public partial class MainWindow : Window
{
    private Player player = new();
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

        // System events
        ScalingChanged += (o, e) => Reposition();
        Screens.Changed += (o, e) => Reposition();
        OS.Current.SessionSwitch += SessionSwitch;

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
            OS.Current.UnMute();
            if(state.RemainingTime is null)
            {
                LabelTimer.Text = "Unlimited";
            }else{
                var timeSpan = TimeSpan.FromSeconds((int)state.RemainingTime);
                LabelTimer.Text = string.Format("{0:D2}:{1:D2}.{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
            }
            if(state.RemainingTime == 600)
            {
                OS.Current.Notify(
                    "Time limit warning",
                    "Your time is up in 10 minutes!"
                );
                player.Play("Resources/alarm.wav");
            }
            else if(state.RemainingTime == 30 && !state.graceGiven)
            {
                OS.Current.Notify(
                    "Time limit warning",
                    "Your time is up in 30 seconds!"
                );
                state.graceGiven = true;
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
    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (state.Paused)
        {
            OS.Current.Mute();
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

    private void SessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if(e.Type == SessionSwitchType.Lock)
        {
            if(!state.Paused) state.TogglePause();
            Timer.Stop();
            OS.Current.UnMute();
        }
        else if(e.Type == SessionSwitchType.Unlock)
        {
            Timer.Start();
            if(state.Paused) OS.Current.Mute();
        }
    }

    private void AboutButton_Click(object? sender, RoutedEventArgs e)
    {
        aboutWindow ??= new AboutWindow();
        aboutWindow.Show();
    }
    private void PauseButton_Click(object? sender, RoutedEventArgs e)
    {
        state.TogglePause();
    }
    private void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        var controlPanel = new ControlPanel(this, state);
        controlPanel.ShowDialog(this);
    }
    private void LogoffButton_Click(object? sender, RoutedEventArgs e)
    {
        OS.Current.SaveState(state.state);
        Timer.Stop();
        state.userIntent = UserIntent.Exit;
        OS.Current.Logoff();
        Close();
        
    }
    private void ShutdownButton_Click(object? sender, RoutedEventArgs e)
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