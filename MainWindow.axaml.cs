using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
//using ManagedBass;
using Microsoft.Win32;
using System.Diagnostics;

namespace AutoLogout;

public partial class MainWindow : Window
{
    public State state = new();

    private readonly DispatcherTimer timer;
    private readonly int alarmStream;

    private readonly LockoutWindow lockoutWindow;
    public ControlPanel? controlPanel;
    private readonly AudioControl audioControl;

    private bool sessionswitch_restoreMainTimer = false;
    private bool sessionswitch_restoreAudioTimer = false;

    public MainWindow()
    {
        InitializeComponent();

        // Set tooltips
        ToolTip.SetTip(LogoffButton, "Log off");
        ToolTip.SetTip(ShutdownButton, "Shut down");
        ToolTip.SetTip(SettingsButton, "Settings");

        // Attach events
        PauseButton.Click += PauseButton_Click;
        LogoffButton.Click += LogOff;
        ShutdownButton.Click += ShutDown;
        SettingsButton.Click += SettingsButton_Click;

        // Window properties
        Title = "Time limit";
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        SystemDecorations = SystemDecorations.None;
        Width = 240;
        Height = 144;

        // Position the window
        Reposition();

        // Events
        Opened += OnOpened;
        //TODO: make this multiplatform
        #if WINDOWS
        SystemEvents.DisplaySettingsChanged += Reposition;
        SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        #endif
        // ToastNotificationManagerCompat.OnActivated += FocusWindow; // TODO: Port to Avalonia

        timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        timer.Tick += Timer_Tick;

        state.Changed += OnStateChanged;

        lockoutWindow = new LockoutWindow(this);
        audioControl = new AudioControl();
        //alarmStream = Bass.CreateStream("Resources/alarm.wav");
    }

    private void Reposition(object? sender, EventArgs? e)
    {
        Reposition();
    }
    private void Reposition()
    {
        var screens = Screens.All;
        if (screens.Count > 0)
        {
            var primary = screens[0];
            Position = new PixelPoint(primary.WorkingArea.Right - (int)Width, primary.WorkingArea.Bottom - (int)Height);
        }
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        int result = await state.FromRegistry();
        if (result < 0)
        {
            state.ExitIntent = true;
            Close();
            return;
        }
        if (state.uuid == Guid.Empty)
        {
            state.uuid = Guid.NewGuid();
            if (await state.NewPassword())
            {
                await MessageBoxManager.GetMessageBoxStandard(
                  "Welcome to AutoLogout",
                  "Password set! Open the control panel to set the rules for this account.",
                  ButtonEnum.Ok,
                  MsBox.Avalonia.Enums.Icon.Info
                ).ShowAsync();
            }
            else
            {
                await MessageBoxManager.GetMessageBoxStandard(
                  "Error",
                  "You must set a password to use this application.",
                  ButtonEnum.Ok,
                  MsBox.Avalonia.Enums.Icon.Error
                ).ShowAsync();
                state.ExitIntent = true;
                Close();
            }
        }

        if (state.remainingTime <= 30 && state.remainingTime != -1)
        {
            await MessageBoxManager.GetMessageBoxStandard(
              "You're about to be logged out!",
              "You're out of time for today. Logging out in 30 seconds.",
              ButtonEnum.Ok,
              MsBox.Avalonia.Enums.Icon.Error
            ).ShowAsync();
            state.tempTimeLimit = state.usedTime + 30;
            state.graceGiven = true;
        }

        EnforceBedtime();
        UpdateClock();
        await Task.Run(state.Sync);
        timer.Start();
    }
    private void OnStateChanged()
    {
        state.tempTimeLimit = -1;
        UpdateClock();
        if (state.syncAuthor.HasValue)
        {
            Notification notif = new() {
                Title = "Time limit changed",
                Message = "Your time limit rules have been changed remotely."
            };
            Common.notificationManager.Show(notif);
        }
    }
    private void PauseButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!state.Paused)
        {
            state.Paused = true;
            PauseButton.Content = "Resume";
            lockoutWindow.Show();
            audioControl.Mute();
            SettingsButton.IsEnabled = false;
        }
        else
        {
            state.Paused = false;
            lockoutWindow.Hide();
            Topmost = false;
            PauseButton.Content = "Pause";
            audioControl.Unmute();
            SettingsButton.IsEnabled = true;
        }
    }
    private async void SettingsButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (controlPanel != null) return;
        if (await state.CheckPassword())
        {
            state.Paused = true;
            controlPanel = new ControlPanel(this);
            await controlPanel.ShowDialog(this);
            state.Paused = false;
        }
    }
    private async void Timer_Tick(object? sender, EventArgs e)
    {
        DateOnly currentDay = DateOnly.FromDateTime(DateTime.Now);
        if (state.usageDate != currentDay)
        {
            state.todayTimeLimit = state.dailyTimeLimit;
            state.usedTime = 0;
            state.usageDate = currentDay;
        }

        if (state.Paused)
        {
            textTimer.IsVisible = !textTimer.IsVisible;
            return;
        }
        else
        {
            textTimer.IsVisible = true;
        }

        state.usedTime++;
        EnforceBedtime();

        if (state.usedTime % 10 == 0)
        {
            await state.SaveToRegistry();
            await Task.Run(state.Sync);
        }

        if (state.remainingTime == -1) return;
        if (state.remainingTime > 0)
        {
            if (state.remainingTime == 600)
            {
                //Bass.ChannelPlay(alarmStream);
                Notification notif = new() {
                    Title = "Time limit warning",
                    Message = "Your time is up in 10 minutes!"
                };
                Common.notificationManager.Show(notif);
            }
            else if (state.remainingTime == 580)
            {
                //Bass.ChannelStop(alarmStream);
                //Bass.StreamFree(alarmStream);
            }
            else if (state.remainingTime == 30 && !state.graceGiven)
            {
                Notification notif = new() {
                    Title = "Time limit warning",
                    Message = "Your time is up in 30 seconds!"
                };
                Common.notificationManager.Show(notif);
                state.graceGiven = true;
            }
        }
        else
        {
            double? remainingTime = CheckBedtime();
            if (remainingTime != null && remainingTime <= 10)
                await ShutDown();
            else
                await LogOff();
        }
        UpdateClock();
    }
    #if WINDOWS
    private void SystemEvents_SessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            if (timer.IsEnabled)
            {
                timer.Stop();
                sessionswitch_restoreMainTimer = true;
            }
            if (audioControl.timer.Enabled)
            {
                audioControl.Unmute();
                sessionswitch_restoreAudioTimer = true;
            }
        }
        else if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            if (sessionswitch_restoreMainTimer)
            {
                timer.Start();
                sessionswitch_restoreMainTimer = false;
            }
            if (sessionswitch_restoreAudioTimer)
            {
                audioControl.Mute();
                sessionswitch_restoreAudioTimer = false;
            }
        }
    }
    #endif

    public void UpdateClock()
    {
        if (state.remainingTime == -1)
        {
            textTimer.Text = "No limit";
        }
        else
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(state.remainingTime);
            string timeString = string.Format("{0:D2}:{1:D2}:{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
            textTimer.Text = timeString;
        }
    }

    private double? CheckBedtime()
    {
        if (state.bedtime == state.waketime)
            return null;
        DateTime now = DateTime.Now;
        DateTime nextBedTime = new(now.Year, now.Month, now.Day, state.bedtime.Hour, state.bedtime.Minute, 0);
        if (state.bedtime.Hour < 12) nextBedTime = nextBedTime.AddDays(1);
        DateTime nextWakeTime = new(now.Year, now.Month, now.Day, state.waketime.Hour, state.waketime.Minute, 0);

        if (now > nextWakeTime)
        {
            if (nextBedTime < nextWakeTime) nextBedTime = nextBedTime.AddDays(1);
        }
        else
        {
            if (now < nextBedTime) nextBedTime = nextBedTime.AddDays(-1);
        }
        return (nextBedTime - now).TotalSeconds;
    }

    private async void EnforceBedtime()
    {
        double? differenceInSeconds = CheckBedtime();

        if (differenceInSeconds == null)
        {
            state.bedtimeTimeLimit = -1;
            UpdateClock();
            return;
        }
        else if (differenceInSeconds < 0)
        {
            if (state.graceGiven) return;
            await MessageBoxManager.GetMessageBoxStandard(
                "AutoLogout",
                "It's past bedtime! Shutting down in 30 seconds.",
                ButtonEnum.Ok,
                MsBox.Avalonia.Enums.Icon.Info
            ).ShowAsync();
            FocusWindow();
            state.bedtimeTimeLimit = state.usedTime + 30;
            state.graceGiven = true;
        }
        else if (state.todayTimeLimit == -1)
        {
            state.bedtimeTimeLimit = state.usedTime + (int)differenceInSeconds;
        }
        else if (differenceInSeconds != state.bedtimeTimeLimit + state.usedTime)
        {
            if ((state.remainingTime - (int)differenceInSeconds) > 60)
            {
                Notification notif = new() {
                    Title = "Time limit warning",
                    Message = "Your time has been shortened so it will end with bedtime."
                };
                Common.notificationManager.Show(notif);
            }
            state.bedtimeTimeLimit = state.usedTime + (int)differenceInSeconds;
        }
    }

    public void ReassertTopMost()
    {
        Topmost = false;
        Topmost = true;
    }

    public void FocusWindow()
    {
        Activate();
    }

    private async void LogOff(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await LogOff();
    }
    private async Task LogOff()
    {
        await state.SaveToRegistry();
        timer.Stop();
        PauseButton.IsEnabled = false;
        state.ExitIntent = true;
#if DEBUG
        await MessageBoxManager.GetMessageBoxStandard("Log out", "AutoLogout").ShowAsync();
#else
        Process.Start("shutdown", "/l /f");
#endif
        Close();
    }

    private async void ShutDown(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ShutDown();
    }
    private async Task ShutDown()
    {
        await state.SaveToRegistry();
        timer.Stop();
        PauseButton.IsEnabled = false;
        state.ExitIntent = true;
#if DEBUG
        await MessageBoxManager.GetMessageBoxStandard("Shut down", "AutoLogout").ShowAsync();
#else
        Process.Start("shutdown", "/p /f");
#endif
        Close();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (!state.ExitIntent)
            e.Cancel = true;
        else
        {
            audioControl.Unmute();
            #if WINDOWS
            SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
            SystemEvents.DisplaySettingsChanged -= Reposition;
            #endif
        }
    }
}