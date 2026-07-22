using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using NetCoreAudio;
using Avalonia.Media;
using System.Diagnostics;

namespace AutoLogout;

public partial class MainWindow : Window
{
    private Player player = new();
    private UserIntent? _lastIntent = null;
    public State state = new();
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
            PauseButtonText.Text = "Resume";
            PauseButtonIcon.IsVisible = false;
            ResumeButtonIcon.IsVisible = true;
            lockoutWindow ??= new LockoutWindow(this);
            lockoutWindow.Show();
        }
        else
        {
            lockoutWindow?.Hide();
            PauseButtonText.Text = "Pause";
            PauseButtonIcon.IsVisible = true;
            ResumeButtonIcon.IsVisible = false;
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
                _ = player.Play("Resources/alarm.wav");
            }
            else if(state.RemainingTime == 30 && state.userIntent != UserIntent.Grace)
            {
                var alert = new AlertDialog(
                    "Your time is up in 30 seconds!",
                    "Time limit warning"
                );
                _ = alert.ShowDialog(this);
                state.userIntent = UserIntent.Grace;
            }
            else if(state.RemainingTime <= 0)
            {
                // User is out of time
                var pastBedtime = state.TimeUntilBedtime is not null && state.TimeUntilBedtime <= 0;
                if(state.userIntent != UserIntent.Grace)
                {
                    var alert = new AlertDialog(
                        pastBedtime?
                            "It's past your bedtime! Shutting down in 30 seconds."
                        :
                            "You're out of time for today! Logging out in 30 seconds.",
                        "AutoLogout"
                    );
                    _ = alert.ShowDialog(this);
                    state.userIntent = UserIntent.Grace;
                    state.tempTimeLimit = state.state.usedTime + 30;
                }
                else
                {
                    if(pastBedtime)
                        Shutdown();
                    else
                        Logoff();
                }
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
        var aboutWindow = new AboutWindow();
        aboutWindow.Show();
    }
    private void PauseButton_Click(object? sender, RoutedEventArgs e)
    {
        state.TogglePause();
    }
    private async void SettingsButton_Click(object? sender, RoutedEventArgs e)
    {
        var prompt = new PromptDialog(
            "Enter the parent password to continue", "AutoLogout Settings", true
        );
        await prompt.ShowDialog(this);
        if(prompt.Result is not null)
            AuthenticateSettings_Callback(prompt.Result);
    }
    public async void AuthenticateSettings_Callback(string password)
    {
        if(!state.CheckPassword(password)) {
            var alert = new AlertDialog("The parent password you provided is incorrect.", "ControlPanel");
            _ = alert.ShowDialog(this);
            return;
        }

        // Change userIntent to parent to show that the parent is authorized
        _lastIntent = state.userIntent;
        state.userIntent = UserIntent.Parent;

        var controlPanel = new ControlPanel(this, state);
        await controlPanel.ShowDialog(this);

        // Upon closing of ControlPanel, revert userIntent
        if(state.userIntent == UserIntent.Exit)
            Close();
        else if(_lastIntent == UserIntent.Grace)
        {
            // Shut down / sign out warnings should be reset after the parent has opened settings
            state.tempTimeLimit = null;
            state.userIntent = UserIntent.None;
        }
        else if(_lastIntent is not null)
        {
            state.userIntent = (UserIntent)_lastIntent;
        }
    }
    private void LogoffButton_Click(object? sender, RoutedEventArgs e)
    {
        Logoff();
    }
    private void Logoff()
    {
        OS.Current.SaveState(state.state);
        Timer.Stop();
        state.userIntent = UserIntent.Exit;
        OS.Current.Logoff();
        Close();
    }
    private void ShutdownButton_Click(object? sender, RoutedEventArgs e)
    {
        Shutdown();
    }
    private void Shutdown()
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