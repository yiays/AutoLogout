using System.Diagnostics;
using System.Media;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AutoLogout
{
 public partial class CountdownTimer : Form
  {
    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    private readonly Panel mainPanel;
    private readonly FlowLayoutPanel buttonPanel;
    private readonly Label textTimer;
    private readonly Button pauseButton;
    private readonly Button logoffButton;
    private readonly Button shutdownButton;
    private readonly Button settingsButton;
    private readonly System.Windows.Forms.Timer timer;
    private readonly NotifyIcon notifyIcon;

    // Config defaults
    private String password = "";
    public int dailyTimeLimit;
    public int remainingTime;
    private DateOnly remainingTimeDay = new(1, 1, 1);
    public TimeOnly bedtime = new(0, 0);
    public TimeOnly waketime = new(0, 0);
    private bool graceGiven = false;

    private readonly LockoutWindow lockoutWindow;
    public ControlPanel? controlPanel;
    private readonly AudioControl audioControl;

    public CountdownTimer() {
      Text = "Time limit";
      FormBorderStyle = FormBorderStyle.FixedToolWindow;
      ShowInTaskbar = false;
      StartPosition = FormStartPosition.Manual;
      Width = 206;
      Height = 130;
      ControlBox = false; // No control buttons
      AutoScaleMode = AutoScaleMode.Dpi;
      AutoScaleDimensions = new SizeF(125F, 125F);
      MaximizeBox = false;

      // Events
      Reposition(null, null);
      SystemEvents.DisplaySettingsChanged += Reposition;

      Load += OnLoad;

      timer = new System.Windows.Forms.Timer
      {
        Interval = 1000
      };
      timer.Tick += Timer_Tick;

      // Form elements
      mainPanel = new Panel
      {
        Dock = DockStyle.Top,
        AutoSize = true,
      };
      buttonPanel = new FlowLayoutPanel
      {
        Dock = DockStyle.Bottom,
        FlowDirection = FlowDirection.LeftToRight,
        AutoSize = true
      };

      textTimer = new Label
      {
        Location = new Point(0, -10),
        AutoSize = true,
        Font = new Font("Segoe UI", 22, FontStyle.Bold)
      };

      pauseButton = new Button
      {
        Text = "Pause",
        Width = 90,
        Height = 32,
      };
      pauseButton.Click += Pause;

      IntPtr logofficonHandle = ExtractIcon(IntPtr.Zero, "shell32.dll", 44);
      Icon logoffIcon = Icon.FromHandle(logofficonHandle);
      Bitmap logoffBitmap = new Bitmap(logoffIcon.ToBitmap(), new Size(24, 24));
      logoffButton = new Button {
        Image = logoffBitmap,
        AccessibleDescription = "Log off",
        Width = 32,
        Height = 32,
      };
      logoffButton.Click += LogOff;
      ToolTip logoffHint = new ToolTip();
      logoffHint.SetToolTip(logoffButton, "Log off");

      IntPtr shutdowniconHandle = ExtractIcon(IntPtr.Zero, "shell32.dll", 27);
      Icon shutdownIcon = Icon.FromHandle(shutdowniconHandle);
      Bitmap shutdownBitmap = new Bitmap(shutdownIcon.ToBitmap(), new Size(24, 24));
      shutdownButton = new Button {
        Image = shutdownBitmap,
        AccessibleDescription = "Shutdown",
        Width = 32,
        Height = 32,
      };
      shutdownButton.Click += ShutDown;
      ToolTip shutdownHint = new ToolTip();
      shutdownHint.SetToolTip(shutdownButton, "Shut down");

      IntPtr settingsiconHandle = ExtractIcon(IntPtr.Zero, "shell32.dll", 21);
      Icon settingsIcon = Icon.FromHandle(settingsiconHandle);
      Bitmap settingsBitmap = new Bitmap(settingsIcon.ToBitmap(), new Size(24, 24));
      settingsButton = new Button
      {
        Image = settingsBitmap,
        AccessibleDescription = "Settings",
        Width = 32,
        Height = 32,
      };
      settingsButton.Click += Settings;
      ToolTip settingsHint = new ToolTip();
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

      notifyIcon = new NotifyIcon() {
        Icon = new Icon("Resources/icon.ico"),
        Visible = true,
        Text = "Show time limit"
      };
      notifyIcon.Click += FocusWindow;
    }

    private void LoadFromRegistry() {
      RegistryKey? key = Registry.CurrentUser.CreateSubKey("Software\\Yiays\\AutoLogout", true);
      if (key == null)
      {
        MessageBox.Show("Unable to access settings.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        remainingTime = 0;
        Close();
        return;
      }

      // Load current app state from registry
      password = (string)key.GetValue("password", "");

      string bedtimeRaw = (string)key.GetValue("bedtime", "0:00");
      bedtime = TimeOnly.Parse(bedtimeRaw);
      string waketimeRaw = (string)key.GetValue("waketime", "0:00");
      waketime = TimeOnly.Parse(waketimeRaw);

      dailyTimeLimit = (int)key.GetValue("dailyTimeLimit", -1);
      remainingTimeDay = DateOnly.Parse((String)key.GetValue("remainingTimeDay", "1/01/0001"));
      remainingTime = (int)key.GetValue("remainingTime", -1);
    }

    private void SaveToRegistry() {
      RegistryKey? key = Registry.CurrentUser.CreateSubKey("Software\\Yiays\\AutoLogout");
      if (key != null)
      {
        key.SetValue("password", password);
        key.SetValue("remainingTimeDay", DateOnly.FromDateTime(DateTime.Today));
        key.SetValue("dailyTimeLimit", dailyTimeLimit);
        key.SetValue("remainingTime", remainingTime);
        key.SetValue("bedtime", bedtime.ToString());
        key.SetValue("waketime", waketime.ToString());
      }
    }

    private void Reposition(object? sender, EventArgs? e)
    {
      // Calculate the position just offset from the bottom right corner
      if (Screen.PrimaryScreen != null)
      {
        Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(workingArea.Right - Width, workingArea.Bottom - Height);
      }
    }

    private void OnLoad(object? sender, EventArgs e) {
      LoadFromRegistry();
      if (password == "")
      {
        string? newPassword = Prompt.ShowDialog("Enter a new parent password.", "Welcome to AutoLogout");
        if (newPassword == null)
        {
          MessageBox.Show("You must set a password to use this application.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
          remainingTime = 0;
          Close();
          return;
        }
        password = newPassword;
        SaveToRegistry();
        MessageBox.Show("Password set! Open the control panel to set the rules for this account.", "Welcome to AutoLogout", MessageBoxButtons.OK, MessageBoxIcon.Information);
      }

      DateOnly currentDay = DateOnly.FromDateTime(DateTime.Now);
      if (remainingTimeDay != currentDay)
      {
        // If the day is different, reset the remaining time
        remainingTime = dailyTimeLimit;
        remainingTimeDay = currentDay;
      }
      UpdateClock();
      EnforceBedtime();
      timer.Start();
    }

    private void Pause(object? sender, EventArgs e) {
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
        audioControl.Mute(null, null);
        settingsButton.Enabled = false;
      }
      else
      {
        lockoutWindow.Hide();
        TopMost = false;
        pauseButton.Text = "Pause";
        if (remainingTime >= 0)
        {
          timer.Start();
          if (remainingTime > 0) remainingTime--;
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
      string? password = Prompt.ShowDialog("Enter the parent password to continue.", "AutoLogout Settings");
      if (password == null) return;

      if (password == this.password)
      {
        controlPanel = new ControlPanel(this);
        controlPanel.ShowDialog();
      }
      else MessageBox.Show("The password was incorrect", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
      UpdateClock();
      if (remainingTime == -1) // Unlimited time
        return;
      if (remainingTime > 0)
      {
        remainingTime--;
        if (remainingTime % 10 == 0)
        {
          // Save to the registry every 10 seconds
          SaveToRegistry();
          Console.WriteLine("Writing to registry...");
        }
        EnforceBedtime();
        if (remainingTime == 600)
        {
          SoundPlayer player = new SoundPlayer("Resources/alarm.wav");
          player.Play();
          Task.Run(() =>
          {
            MessageBox.Show(
            "Your time is up in 10 minutes!", "Time limit warning", MessageBoxButtons.OK, MessageBoxIcon.Warning
            );
          });
        }
        if (remainingTime == 30 && !graceGiven)
        {
          Task.Run(() =>
          {
            MessageBox.Show(
            "Your time is up in 30 seconds!", "Time limit warning", MessageBoxButtons.OK, MessageBoxIcon.Warning
            );
          });
          graceGiven = true;
        }
      }
      else
      {
        timer.Stop();
        double? remainingTime = CheckBedtime();
        if (remainingTime != null && remainingTime <= 10)
          ShutDown(null, null);
        else
          LogOff(null, null);
      }
    }

    private void UpdateClock() {
      if (remainingTime == -1) // Unlimited time
      {
        textTimer.Text = "No limit";
        return;
      }
      TimeSpan timeSpan = TimeSpan.FromSeconds(remainingTime);
      string timeString = string.Format("{0:D2}:{1:D2}:{2:D2}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds);
      textTimer.Text = timeString;
    }

    private double? CheckBedtime() {
      if (bedtime == waketime)
        return null;
      DateTime now = DateTime.Now;
      // Create sleepTime and wakeTime, assuming both are for today
      DateTime nextBedTime = new(now.Year, now.Month, now.Day, bedtime.Hour, bedtime.Minute, 0);
      if(bedtime.Hour < 12) nextBedTime = nextBedTime.AddDays(1);
      DateTime nextWakeTime = new(now.Year, now.Month, now.Day, waketime.Hour, waketime.Minute, 0);

      // Correct sleepTime and wakeTime based on the current time
      if(now > nextWakeTime) {
        // if sleepTime is before wakeTime, it will next occur tomorrow
        if(nextBedTime < nextWakeTime) nextBedTime = nextBedTime.AddDays(1);
      }else{
        // sleepTime must be before wakeTime if wakeTime hasn't passed yet
        if(now < nextBedTime) nextBedTime = nextBedTime.AddDays(-1);
      }
      return (nextBedTime - now).TotalSeconds;
      }

    private void EnforceBedtime() {
      double? differenceInSeconds = CheckBedtime();
      
      if (differenceInSeconds == null) return;
      else if (differenceInSeconds < 0)
      {
        if (graceGiven) return;
        Task.Run(() =>
        {
          MessageBox.Show("It's past bedtime! Shutting down in 30 seconds.");
          Invoke(() => FocusWindow(null, null));
        });
        remainingTime = 30;
        graceGiven = true;
      }
      else if (remainingTime == -1) // Unlimited time
      {
        // No time limit, just bedtime
        remainingTime = (int)differenceInSeconds;
      }
      else if (differenceInSeconds < remainingTime)
      {
        if (Math.Abs((decimal)remainingTime - (decimal)differenceInSeconds) > 60)
        {
          // Only alert if the difference is more than a minute
          Task.Run(() =>
          {
            MessageBox.Show("Your time has been shortened so it will end with bedtime.");
            Invoke(() => FocusWindow(null, null));
          });
        }
        remainingTime = (int)differenceInSeconds;
      }
    }

    public void ReassertTopMost(object? sender, EventArgs? e) {
      TopMost = false;
      TopMost = true;
    }

    public void FocusWindow(object? sender, EventArgs? e) {
      Activate();
    }

    private void LogOff(object? sender, EventArgs? e) {
      SaveToRegistry();
      timer.Stop();
      pauseButton.Enabled = false;
      remainingTime = 0;
      if (Debugger.IsAttached)
      {
        MessageBox.Show("Log off");
        return;
      }
      Process.Start("shutdown", "/l /f");
      Application.Exit();
    }

    private void ShutDown(object? sender, EventArgs? e) {
      SaveToRegistry();
      timer.Stop();
      pauseButton.Enabled = false;
      remainingTime = 0;
      if (Debugger.IsAttached)
      {
        MessageBox.Show("Shut down");
        return;
      }
      Process.Start("shutdown", "/p /f");
      Application.Exit();
    }

    protected override void OnFormClosing(FormClosingEventArgs e) {
      if(remainingTime == -1 || remainingTime > 0)
        e.Cancel = true;
      }
  }
}
