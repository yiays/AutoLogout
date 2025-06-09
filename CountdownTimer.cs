using System.Diagnostics;
using System.Media;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Toolkit.Uwp.Notifications;
using BC = BCrypt.Net;

namespace AutoLogout
{
  public partial class CountdownTimer : Form
  {
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    private readonly Label textTimer;
    private readonly Button pauseButton;
    private readonly Button logoffButton;
    private readonly Button shutdownButton;
    private readonly Button settingsButton;
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

      Text = "Time limit";
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

      pauseButton = new Button
      {
        Text = "Pause",
        AutoSize = true,
        Font = new Font("Segoe UI", 12)
      };
      pauseButton.Click += Pause;

      IntPtr logofficonHandle = ExtractIcon(IntPtr.Zero, "shell32.dll", 44);
      Icon logoffIcon = Icon.FromHandle(logofficonHandle);
      Bitmap logoffBitmap = new Bitmap(logoffIcon.ToBitmap(), IconScale);
      logoffButton = new Button
      {
        Image = logoffBitmap,
        AccessibleDescription = "Log off",
        Width = pauseButton.PreferredSize.Height,
        Height = pauseButton.PreferredSize.Height,
      };
      logoffButton.Click += LogOff;
      ToolTip logoffHint = new();
      logoffHint.SetToolTip(logoffButton, "Log off");

      IntPtr shutdowniconHandle = ExtractIcon(IntPtr.Zero, "shell32.dll", 27);
      Icon shutdownIcon = Icon.FromHandle(shutdowniconHandle);
      Bitmap shutdownBitmap = new(shutdownIcon.ToBitmap(), IconScale);
      shutdownButton = new Button
      {
        Image = shutdownBitmap,
        AccessibleDescription = "Shutdown",
        Width = pauseButton.PreferredSize.Height,
        Height = pauseButton.PreferredSize.Height,
      };
      shutdownButton.Click += ShutDown;
      ToolTip shutdownHint = new();
      shutdownHint.SetToolTip(shutdownButton, "Shut down");

      IntPtr settingsiconHandle = ExtractIcon(IntPtr.Zero, "shell32.dll", 21);
      Icon settingsIcon = Icon.FromHandle(settingsiconHandle);
      Bitmap settingsBitmap = new(settingsIcon.ToBitmap(), IconScale);
      settingsButton = new Button
      {
        Image = settingsBitmap,
        AccessibleDescription = "Settings",
        Width = pauseButton.PreferredSize.Height,
        Height = pauseButton.PreferredSize.Height,
      };
      settingsButton.Click += Settings;
      ToolTip settingsHint = new();
      settingsHint.SetToolTip(settingsButton, "Settings");

      mainPanel.Controls.Add(textTimer);
      buttonPanel.Controls.Add(pauseButton);
      buttonPanel.Controls.Add(logoffButton);
      buttonPanel.Controls.Add(shutdownButton);
      buttonPanel.Controls.Add(settingsButton);

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

    private void OnLoad(object? sender, EventArgs e)
    {
      int result = State.LoadFromRegistry();
      if (result < 0)
      {
        State.remainingTime = 0;
        Close();
        return;
      }
      if (State.guid == Guid.Empty)
      {
        State.guid = Guid.NewGuid();
        string? newPassword = Prompt.ShowDialog("Enter a new parent password.", "Welcome to AutoLogout", true);
        if (newPassword == null)
        {
          MessageBox.Show("You must set a password to use this application.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
          State.remainingTime = 0;
          Close();
          return;
        }
        State.hashedPassword = BC.BCrypt.HashPassword(newPassword);
        State.SaveToRegistry();
        MessageBox.Show("Password set! Open the control panel to set the rules for this account.", "Welcome to AutoLogout", MessageBoxButtons.OK, MessageBoxIcon.Information);
      }

      if (State.remainingTime < 30)
      {
        Task.Run(() =>
        {
          MessageBox.Show(
            "You're out of time for today. Logging out in 30 seconds.",
            "You're about to be logged out!",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
        });
        State.remainingTime = 30;
        State.graceGiven = true;
      }

      UpdateClock();
      EnforceBedtime();
      timer.Start();
    }

    private void Pause(object? sender, EventArgs e)
    {
      // Pause the timer
      if (timer.Enabled)
      {
        timer.Stop();
        pauseButton.Text = "Resume";
        if (controlPanel != null)
        {
          controlPanel.Close();
          controlPanel = null;
        }
        lockoutWindow.Show();
        audioControl.Mute();
        settingsButton.Enabled = false;
      }
      else
      {
        lockoutWindow.Hide();
        TopMost = false;
        pauseButton.Text = "Pause";
        if (State.remainingTime >= 0)
        {
          timer.Start();
          if (State.remainingTime > 0)
          {
            State.remainingTime--;
            State.usedTime++;
          }
          EnforceBedtime();
          UpdateClock();
        }
        audioControl.Unmute();
        settingsButton.Enabled = true;
      }
    }

    private void Settings(object? sender, EventArgs e)
    {
      if (controlPanel != null) return;
      string? password = Prompt.ShowDialog("Enter the parent password to continue.", "AutoLogout Settings", true);
      if (password == null) return;

      if (BC.BCrypt.Verify(password, State.hashedPassword))
      {
        timer.Stop();
        controlPanel = new ControlPanel(this);
        controlPanel.ShowDialog();
        timer.Start();
      }
      else MessageBox.Show("The password was incorrect", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
      DateOnly currentDay = DateOnly.FromDateTime(DateTime.Now);
      if (State.remainingTimeDay != currentDay)
      {
        // If the day is different, reset the remaining time
        State.remainingTime = State.dailyTimeLimit;
        State.usedTime = 0;
        State.remainingTimeDay = currentDay;
      }
      if (State.remainingTime == -1) // Unlimited time
        return;
      if (State.remainingTime > 0)
      {
        State.remainingTime--;
        State.usedTime++;
        if (State.remainingTime % 10 == 0)
        {
          // Save to the registry and sync every 10 seconds
          State.SaveToRegistry();
          Task.Run(State.Sync);
        }
        EnforceBedtime();
        if (State.remainingTime == 600)
        {
          player.Play();
          new ToastContentBuilder()
            .AddText("Time limit warning")
            .AddText("Your time is up in 10 minutes!")
            .Show();
        }
        else if (State.remainingTime == 580)
        {
          player.Dispose();
        }
        else if (State.remainingTime == 30 && !State.graceGiven)
        {
          new ToastContentBuilder()
            .AddText("Time limit warning")
            .AddText("Your time is up in 30 seconds!")
            .Show();
          State.graceGiven = true;
        }
      }
      else
      {
        timer.Stop();
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
      if (State.remainingTime == -1) // Unlimited time
      {
        textTimer.Text = "No limit";
        return;
      }
      TimeSpan timeSpan = TimeSpan.FromSeconds(State.remainingTime);
      string timeString = string.Format("{0:D2}:{1:D2}:{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
      textTimer.Text = timeString;
    }

    private double? CheckBedtime()
    {
      if (State.bedtime == State.waketime)
        return null;
      DateTime now = DateTime.Now;
      // Create sleepTime and wakeTime, assuming both are for today
      DateTime nextBedTime = new(now.Year, now.Month, now.Day, State.bedtime.Hour, State.bedtime.Minute, 0);
      if (State.bedtime.Hour < 12) nextBedTime = nextBedTime.AddDays(1);
      DateTime nextWakeTime = new(now.Year, now.Month, now.Day, State.waketime.Hour, State.waketime.Minute, 0);

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

      if (differenceInSeconds == null) return;
      else if (differenceInSeconds < 0)
      {
        if (State.graceGiven) return;
        Task.Run(() =>
        {
          MessageBox.Show("It's past bedtime! Shutting down in 30 seconds.");
          Invoke(FocusWindow);
        });
        State.remainingTime = 30;
        State.graceGiven = true;
      }
      else if (State.remainingTime == -1) // Unlimited time
      {
        // No time limit, just bedtime
        State.remainingTime = (int)differenceInSeconds;
      }
      else if (differenceInSeconds < State.remainingTime)
      {
        if (Math.Abs(State.remainingTime - (decimal)differenceInSeconds) > 60)
        {
          // Only alert if the difference is more than a minute
          new ToastContentBuilder()
            .AddText("Time limit warning")
            .AddText("Your time has been shortened so it will end with bedtime.")
            .Show();
        }
        State.remainingTime = (int)differenceInSeconds;
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
      State.SaveToRegistry();
      timer.Stop();
      pauseButton.Enabled = false;
      State.remainingTime = 0;
      if (Debugger.IsAttached)
      {
        MessageBox.Show("Log off");
        return;
      }
      Process.Start("shutdown", "/l /f");
      Application.Exit();
    }

    private void ShutDown(object? sender, EventArgs? e)
    {
      ShutDown();
    }
    private void ShutDown()
    {
      State.SaveToRegistry();
      timer.Stop();
      pauseButton.Enabled = false;
      State.remainingTime = 0;
      if (Debugger.IsAttached)
      {
        MessageBox.Show("Shut down");
        return;
      }
      Process.Start("shutdown", "/p /f");
      Application.Exit();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
      if (State.remainingTime == -1 || State.remainingTime > 0)
        e.Cancel = true;
      else
      {
        SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
        SystemEvents.DisplaySettingsChanged -= Reposition;
        ToastNotificationManagerCompat.OnActivated -= FocusWindow;
      }
    }
  }
}
