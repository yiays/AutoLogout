using System.Diagnostics;
using System.Media;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Toolkit.Uwp.Notifications;

namespace AutoLogout
{
  public partial class CountdownTimer : Form
  {
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    public State state = new();

    private readonly Label textTimer;
    private readonly Button PauseButton;
    private readonly Button LogoffButton;
    private readonly Button ShutdownButton;
    private readonly Button SettingsButton;
    private readonly System.Windows.Forms.Timer timer;
    private readonly NotifyIcon notifyIcon;
    private readonly SoundPlayer player = new("Resources/alarm.wav");

    private readonly LockoutWindow lockoutWindow;
    public ControlPanel? controlPanel;
    private readonly AudioControl audioControl;

    private bool sessionswitch_restoreMainTimer = false;
    private bool sessionswitch_restoreAudioTimer = false;

    public CountdownTimer()
    {
      Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

#if DEBUG
      Text = "Time limit [DEBUG]";
#else
      Text = "Time limit";
#endif
      Icon = new Icon("Resources/icon-light.ico");
      FormBorderStyle = FormBorderStyle.FixedToolWindow;
      ShowInTaskbar = false;
      StartPosition = FormStartPosition.Manual;
      ControlBox = false; // No control buttons
      MaximizeBox = false;
      BackColor = Color.White;
      Width = 240;
      Height = 144;

      AutoScaleMode = AutoScaleMode.Dpi;
      AutoScaleDimensions = new(96F, 96F);

      Size IconScale = (32 * AutoScaleFactor).ToSize();

      // Events
      Reposition();
      SystemEvents.DisplaySettingsChanged += Reposition;
      SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
      ToastNotificationManagerCompat.OnActivated += FocusWindow;
      Load += OnLoad;

      timer = new System.Windows.Forms.Timer
      {
        Interval = 1000
      };
      timer.Tick += Timer_Tick;

      state.Changed += OnStateChanged;

      // Form elements
      Panel mainPanel = new()
      {
        Dock = DockStyle.Top,
        AutoSize = true,
      };
      FlowLayoutPanel buttonPanel = new()
      {
        Dock = DockStyle.Bottom,
        FlowDirection = FlowDirection.LeftToRight,
        AutoSize = true,
        BackColor = SystemColors.Control,
      };

      textTimer = new Label
      {
        Location = new Point(0, -10),
        AutoSize = true,
        Font = new Font("Segoe UI", 36, FontStyle.Bold)
      };

      PauseButton = new Button
      {
        Text = "Pause",
        AutoSize = true,
        Font = new Font("Segoe UI", 12)
      };
      PauseButton.Click += PauseButton_Click;

      IntPtr logofficonHandle = ExtractIcon(IntPtr.Zero, "shell32.dll", 44);
      Icon logoffIcon = Icon.FromHandle(logofficonHandle);
      Bitmap logoffBitmap = new Bitmap(logoffIcon.ToBitmap(), IconScale);
      LogoffButton = new Button
      {
        Image = logoffBitmap,
        AccessibleDescription = "Log off",
        Width = PauseButton.PreferredSize.Height,
        Height = PauseButton.PreferredSize.Height,
      };
      LogoffButton.Click += LogOff;
      ToolTip logoffHint = new();
      logoffHint.SetToolTip(LogoffButton, "Log off");

      IntPtr shutdowniconHandle = ExtractIcon(IntPtr.Zero, "shell32.dll", 27);
      Icon shutdownIcon = Icon.FromHandle(shutdowniconHandle);
      Bitmap shutdownBitmap = new(shutdownIcon.ToBitmap(), IconScale);
      ShutdownButton = new Button
      {
        Image = shutdownBitmap,
        AccessibleDescription = "Shutdown",
        Width = PauseButton.PreferredSize.Height,
        Height = PauseButton.PreferredSize.Height,
      };
      ShutdownButton.Click += ShutDown;
      ToolTip shutdownHint = new();
      shutdownHint.SetToolTip(ShutdownButton, "Shut down");

      IntPtr settingsiconHandle = ExtractIcon(IntPtr.Zero, "shell32.dll", 21);
      Icon settingsIcon = Icon.FromHandle(settingsiconHandle);
      Bitmap settingsBitmap = new(settingsIcon.ToBitmap(), IconScale);
      SettingsButton = new Button
      {
        Image = settingsBitmap,
        AccessibleDescription = "Settings",
        Width = PauseButton.PreferredSize.Height,
        Height = PauseButton.PreferredSize.Height,
      };
      SettingsButton.Click += SettingsButton_Click;
      ToolTip settingsHint = new();
      settingsHint.SetToolTip(SettingsButton, "Settings");

      mainPanel.Controls.Add(textTimer);
      buttonPanel.Controls.Add(PauseButton);
      buttonPanel.Controls.Add(LogoffButton);
      buttonPanel.Controls.Add(ShutdownButton);
      buttonPanel.Controls.Add(SettingsButton);

      Controls.Add(mainPanel);
      Controls.Add(buttonPanel);

      lockoutWindow = new LockoutWindow(this);
      audioControl = new AudioControl();

      notifyIcon = new NotifyIcon()
      {
        Icon = new Icon("Resources/icon.ico"),
        Visible = true,
        Text = "Show time limit"
      };
      notifyIcon.Click += FocusWindow;
    }

    private void Reposition(object? sender, EventArgs? e)
    {
      Reposition();
    }
    private void Reposition()
    {
      // Calculate the position just offset from the bottom right corner
      if (Screen.PrimaryScreen != null)
      {
        Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(workingArea.Right - Width, workingArea.Bottom - Height);
      }
    }

    // Events
    private void OnLoad(object? sender, EventArgs e)
    {
      int result = state.FromRegistry();
      if (result < 0)
      {
        state.ExitIntent = true;
        Close();
        return;
      }
      if (state.uuid == Guid.Empty)
      {
        state.uuid = Guid.NewGuid();
        if (state.NewPassword())
        {
          MessageBox.Show("Password set! Open the control panel to set the rules for this account.", "Welcome to AutoLogout", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
          MessageBox.Show("You must set a password to use this application.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
          state.ExitIntent = true;
          Close();
        }
      }

      if (state.remainingTime <= 30 && state.remainingTime != -1)
      {
        Task.Run(() =>
        {
          MessageBox.Show(
            "You're out of time for today. Logging out in 30 seconds.",
            "You're about to be logged out!",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        });
        state.tempTimeLimit = state.usedTime + 30;
        state.graceGiven = true;
      }

      EnforceBedtime();
      UpdateClock();
      Task.Run(state.Sync);
      timer.Start();
    }
    private void OnStateChanged()
    {
      if (InvokeRequired)
      {
        Invoke(OnStateChanged);
      }
      else
      {
        state.tempTimeLimit = -1;
        UpdateClock();
        if (state.syncAuthor.HasValue)
        {
          new ToastContentBuilder()
            .AddText("Time limit changed")
            .AddText("Your time limit rules have been changed remotely.")
            .Show();
        }
      }
    }
    private void PauseButton_Click(object? sender, EventArgs e)
    {
      // Pause the timer
      if (!state.Paused)
      {
        state.Paused = true;
        PauseButton.Text = "Resume";
        lockoutWindow.Show();
        audioControl.Mute();
        SettingsButton.Enabled = false;
      }
      else // Unpause
      {
        state.Paused = false;
        lockoutWindow.Hide();
        TopMost = false;
        PauseButton.Text = "Pause";
        audioControl.Unmute();
        SettingsButton.Enabled = true;
      }
    }
    private void SettingsButton_Click(object? sender, EventArgs e)
    {
      if (controlPanel != null) return;
      if (state.CheckPassword())
      {
        state.Paused = true;
        controlPanel = new ControlPanel(this);
        controlPanel.ShowDialog();
        state.Paused = false;
      }
    }
    private void Timer_Tick(object? sender, EventArgs e)
    {
      DateOnly currentDay = DateOnly.FromDateTime(DateTime.Now);
      if (state.usageDate != currentDay)
      {
        // If the day is different, reset the remaining time
        state.todayTimeLimit = state.dailyTimeLimit;
        state.usedTime = 0;
        state.usageDate = currentDay;
      }

      if (state.Paused)
      {
        if (textTimer.Visible) textTimer.Visible = false;
        else textTimer.Visible = true;
        return;
      }
      else
      {
        textTimer.Visible = true;
      }

      state.usedTime++;
      EnforceBedtime();

      // Save to the registry and sync every 10 seconds
      if (state.usedTime % 10 == 0)
      {
        state.SaveToRegistry();
        Task.Run(state.Sync);
      }

      if (state.remainingTime == -1) // Unlimited time
        return;
      if (state.remainingTime > 0)
      {
        if (state.remainingTime == 600)
        {
          player.Play();
          new ToastContentBuilder()
            .AddText("Time limit warning")
            .AddText("Your time is up in 10 minutes!")
            .Show();
        }
        else if (state.remainingTime == 580)
        {
          player.Dispose();
        }
        else if (state.remainingTime == 30 && !state.graceGiven)
        {
          new ToastContentBuilder()
            .AddText("Time limit warning")
            .AddText("Your time is up in 30 seconds!")
            .Show();
          state.graceGiven = true;
        }
      }
      else
      {
        // remainingTime is zero
        double? remainingTime = CheckBedtime();
        if (remainingTime != null && remainingTime <= 10)
          ShutDown();
        else
          LogOff();
      }
      UpdateClock();
    }
    private void SystemEvents_SessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
    {
      if (e.Reason == SessionSwitchReason.SessionLock)
      {
        // The session was locked (Win+L or switch user)
        if (timer.Enabled)
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
        // The session was unlocked
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

    public void UpdateClock()
    {
      if (state.remainingTime == -1) // Unlimited time
      {
        textTimer.Text = "No limit";
      }
      else
      {
        TimeSpan timeSpan = TimeSpan.FromSeconds(state.remainingTime);
        string timeString = string.Format("{0:D2}:{1:D2}:{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
        textTimer.Text = timeString;
      }
      textTimer.Update();
    }

    private double? CheckBedtime()
    {
      // Retuns time until the next downtime
      if (state.bedtime == state.waketime)
        return null;
      DateTime now = DateTime.Now;
      // Create sleepTime and wakeTime, assuming both are for today
      DateTime nextBedTime = new(now.Year, now.Month, now.Day, state.bedtime.Hour, state.bedtime.Minute, 0);
      if (state.bedtime.Hour < 12) nextBedTime = nextBedTime.AddDays(1);
      DateTime nextWakeTime = new(now.Year, now.Month, now.Day, state.waketime.Hour, state.waketime.Minute, 0);

      // Correct sleepTime and wakeTime based on the current time
      if (now > nextWakeTime)
      {
        // if sleepTime is before wakeTime, it will next occur tomorrow
        if (nextBedTime < nextWakeTime) nextBedTime = nextBedTime.AddDays(1);
      }
      else
      {
        // sleepTime must be before wakeTime if wakeTime hasn't passed yet
        if (now < nextBedTime) nextBedTime = nextBedTime.AddDays(-1);
      }
      return (nextBedTime - now).TotalSeconds;
    }

    private void EnforceBedtime()
    {
      double? differenceInSeconds = CheckBedtime();

      if (differenceInSeconds == null)
      {
        // No downtime
        state.bedtimeTimeLimit = -1;
        UpdateClock();
        return;
      }
      else if (differenceInSeconds < 0)
      {
        // Bedtime has already passed
        if (state.graceGiven) return;
        Task.Run(() =>
        {
          MessageBox.Show("It's past bedtime! Shutting down in 30 seconds.");
          Invoke(FocusWindow);
        });
        state.bedtimeTimeLimit = state.usedTime + 30;
        state.graceGiven = true;
      }
      else if (state.todayTimeLimit == -1) // Unlimited time
      {
        // No time limit, just bedtime
        state.bedtimeTimeLimit = state.usedTime + (int)differenceInSeconds;
      }
      else if (differenceInSeconds != state.bedtimeTimeLimit + state.usedTime)
      {
        // Bedtime is different from expected
        if ((state.remainingTime - (int)differenceInSeconds) > 60)
        {
          // Only alert if the difference is more than a minute
          new ToastContentBuilder()
            .AddText("Time limit warning")
            .AddText("Your time has been shortened so it will end with bedtime.")
            .Show();
        }
        state.bedtimeTimeLimit = state.usedTime + (int)differenceInSeconds;
      }
    }

    public void ReassertTopMost(object? sender, EventArgs? e)
    {
      TopMost = false;
      TopMost = true;
    }

    public void FocusWindow(ToastNotificationActivatedEventArgsCompat? args)
    {
      FocusWindow();
    }
    public void FocusWindow(object? sender, EventArgs? e)
    {
      FocusWindow();
    }
    public void FocusWindow()
    {
      Activate();
    }

    private void LogOff(object? sender, EventArgs? e)
    {
      LogOff();
    }
    private void LogOff()
    {
      state.SaveToRegistry();
      timer.Stop();
      PauseButton.Enabled = false;
      state.ExitIntent = true;
#if DEBUG
      MessageBox.Show("Log off");
      state.ExitIntent = true;
#else
      Process.Start("shutdown", "/l /f");
#endif
      Application.Exit();
    }

    private void ShutDown(object? sender, EventArgs? e)
    {
      ShutDown();
    }
    private void ShutDown()
    {
      state.SaveToRegistry();
      timer.Stop();
      PauseButton.Enabled = false;
      state.ExitIntent = true;
#if DEBUG
      MessageBox.Show("Shut down");
#else
      Process.Start("shutdown", "/p /f");
#endif
      Application.Exit();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
      if (!state.ExitIntent)
        e.Cancel = true;
      else
      {
        audioControl.Unmute();
        SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
        SystemEvents.DisplaySettingsChanged -= Reposition;
        ToastNotificationManagerCompat.OnActivated -= FocusWindow;
      }
    }
  }
}
